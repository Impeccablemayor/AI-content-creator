using System.Security.Cryptography;
using Api.Application.Publishing;
using Api.Application.Scheduling;
using Api.Application.Security;
using Api.Application.SocialAccounts;
using Api.Domain.Enums;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Api.Tests;

public sealed class ScheduledPostPublishServiceTests
{
    private static readonly DateTime Now = new(2026, 6, 1, 12, 0, 0, DateTimeKind.Utc);
    private const string ClaimedAt = "2026-06-01T12:00:00Z";
    private const string PlatformAccountId = "acct-1";

    [Fact]
    public async Task A_successful_publish_marks_the_post_published_and_clears_the_claim()
    {
        var store = NewStore();
        store.Accounts.Add(Connected());
        var publisher = new StubPublisher(ContentPlatform.LinkedIn);
        var service = CreateService(store, publisher);

        var result = await service.PublishAsync(store.Candidates[0], Now, CancellationToken.None);

        Assert.Equal(PublishOutcome.Published, result.Outcome);
        Assert.Null(result.FailureReason);
        Assert.Equal("external-1", result.ExternalPostId);
        var mark = Assert.Single(store.PublishedMarks);
        Assert.Equal(store.Candidates[0].ContentPostId, mark.PostId);
        Assert.Equal(Now, mark.ClaimedAt);
        Assert.NotEqual(default, mark.PublishedAt);

        // The platform's own id is what makes the post measurable later, so it
        // has to survive the publish rather than only the result object.
        Assert.Equal("external-1", mark.ExternalPostId);

        Assert.Empty(store.Failures);
        Assert.Equal("decrypted-token", Assert.Single(publisher.AccessTokens));
    }

    /// <summary>
    /// A failure must not change the post's status, only explain it. The claim is
    /// deliberately kept so the existing claim expiry spaces out the retry.
    /// </summary>
    [Fact]
    public async Task A_failed_publish_records_a_reason_and_leaves_the_post_scheduled()
    {
        var store = NewStore();
        store.Accounts.Add(Connected());
        var publisher = new StubPublisher(ContentPlatform.LinkedIn)
        {
            Throws = new SocialProviderUnavailableException("down")
        };
        var service = CreateService(store, publisher);

        var result = await service.PublishAsync(store.Candidates[0], Now, CancellationToken.None);

        Assert.Equal(PublishOutcome.Failed, result.Outcome);
        Assert.Equal(PublishFailureReason.ProviderUnavailable, result.FailureReason);
        Assert.Empty(store.PublishedMarks);
        var failure = Assert.Single(store.Failures);
        Assert.Equal(PublishFailureReason.ProviderUnavailable, failure.Reason);

        // The platform was called; the point is that the post was not advanced.
        Assert.Equal("decrypted-token", Assert.Single(publisher.AccessTokens));
    }

    [Fact]
    public async Task A_workspace_with_no_social_account_fails_with_not_found()
    {
        var store = NewStore();
        var service = CreateService(store, new StubPublisher(ContentPlatform.LinkedIn));

        var result = await service.PublishAsync(store.Candidates[0], Now, CancellationToken.None);

        Assert.Equal(PublishFailureReason.SocialAccountNotFound, result.FailureReason);
    }

    [Fact]
    public async Task A_disconnected_account_is_not_used()
    {
        var store = NewStore();
        store.Accounts.Add(Connected(status: SocialAccountStatus.Disconnected));
        var publisher = new StubPublisher(ContentPlatform.LinkedIn);
        var service = CreateService(store, publisher);

        var result = await service.PublishAsync(store.Candidates[0], Now, CancellationToken.None);

        Assert.Equal(PublishFailureReason.SocialAccountNotConnected, result.FailureReason);
        Assert.Empty(publisher.AccessTokens);
    }

    [Fact]
    public async Task An_expired_account_is_reported_as_expired_rather_than_not_connected()
    {
        var store = NewStore();
        store.Accounts.Add(Connected(status: SocialAccountStatus.Expired));
        var service = CreateService(store, new StubPublisher(ContentPlatform.LinkedIn));

        var result = await service.PublishAsync(store.Candidates[0], Now, CancellationToken.None);

        Assert.Equal(PublishFailureReason.CredentialsExpired, result.FailureReason);
    }

    [Fact]
    public async Task A_connected_account_whose_token_has_expired_is_not_used()
    {
        var store = NewStore();
        store.Accounts.Add(Connected(tokenExpiresAt: DateTime.UtcNow.AddMinutes(-1)));
        var publisher = new StubPublisher(ContentPlatform.LinkedIn);
        var service = CreateService(store, publisher);

        var result = await service.PublishAsync(store.Candidates[0], Now, CancellationToken.None);

        Assert.Equal(PublishFailureReason.CredentialsExpired, result.FailureReason);
        Assert.Empty(publisher.AccessTokens);
    }

    /// <summary>
    /// Picking one of several connected accounts would publish to an account the
    /// workspace never chose, so this must fail rather than guess.
    /// </summary>
    [Fact]
    public async Task Several_connected_accounts_are_ambiguous_rather_than_guessed()
    {
        var store = NewStore();
        store.Accounts.Add(Connected());
        store.Accounts.Add(Connected(platformAccountId: "acct-2"));
        var publisher = new StubPublisher(ContentPlatform.LinkedIn);
        var service = CreateService(store, publisher);

        var result = await service.PublishAsync(store.Candidates[0], Now, CancellationToken.None);

        Assert.Equal(PublishFailureReason.SocialAccountAmbiguous, result.FailureReason);
        Assert.Empty(publisher.AccessTokens);
    }

    [Fact]
    public async Task An_account_with_no_stored_token_fails_as_unavailable()
    {
        var store = NewStore();
        store.Accounts.Add(Connected(accessTokenEncrypted: null));
        var service = CreateService(store, new StubPublisher(ContentPlatform.LinkedIn));

        var result = await service.PublishAsync(store.Candidates[0], Now, CancellationToken.None);

        Assert.Equal(PublishFailureReason.CredentialsUnavailable, result.FailureReason);
    }

    /// <summary>
    /// A key ring mismatch must fail cleanly. The decryption exception is never
    /// rethrown or logged, because it can describe key material.
    /// </summary>
    [Fact]
    public async Task Credentials_that_cannot_be_decrypted_fail_without_leaking_the_error()
    {
        var store = NewStore();
        store.Accounts.Add(Connected());
        var protector = new StubProtector
        {
            AccessTokenFailure = new CryptographicException(
                "key ring xyz is not available")
        };
        var service = CreateService(store, new StubPublisher(ContentPlatform.LinkedIn), protector);

        var result = await service.PublishAsync(store.Candidates[0], Now, CancellationToken.None);

        Assert.Equal(PublishFailureReason.CredentialsUnavailable, result.FailureReason);
        Assert.Equal(
            PublishFailureReason.CredentialsUnavailable,
            Assert.Single(store.Failures).Reason);
    }

    /// <summary>
    /// The isolation guarantee. A post must never be published through an
    /// account belonging to a different workspace, even if a bug upstream
    /// returned one.
    /// </summary>
    [Fact]
    public async Task An_account_owned_by_another_workspace_is_refused()
    {
        var store = NewStore();
        store.Accounts.Add(Connected(workspaceId: Guid.NewGuid()));
        var publisher = new StubPublisher(ContentPlatform.LinkedIn);
        var service = CreateService(store, publisher);

        var result = await service.PublishAsync(store.Candidates[0], Now, CancellationToken.None);

        Assert.Equal(PublishFailureReason.SocialAccountNotFound, result.FailureReason);
        Assert.Empty(publisher.AccessTokens);
    }

    [Fact]
    public async Task A_post_that_is_no_longer_scheduled_is_not_published()
    {
        var store = NewStore();
        store.Payloads[0] = store.Payloads[0] with { Status = ContentPostStatus.Approved };
        store.Accounts.Add(Connected());
        var publisher = new StubPublisher(ContentPlatform.LinkedIn);
        var service = CreateService(store, publisher);

        var result = await service.PublishAsync(store.Candidates[0], Now, CancellationToken.None);

        Assert.Equal(PublishOutcome.Skipped, result.Outcome);
        Assert.Equal(PublishFailureReason.ContentPostNotScheduled, result.FailureReason);
        Assert.Empty(publisher.AccessTokens);
        Assert.Empty(store.Failures);
    }

    [Fact]
    public async Task A_platform_with_no_publisher_fails_as_unsupported()
    {
        var store = NewStore();
        store.Accounts.Add(Connected());
        var service = CreateService(store, publisher: null);

        var result = await service.PublishAsync(store.Candidates[0], Now, CancellationToken.None);

        Assert.Equal(PublishFailureReason.PlatformNotSupported, result.FailureReason);
    }

    [Fact]
    public async Task A_platform_that_is_not_configured_fails_before_any_request()
    {
        var store = NewStore();
        store.Accounts.Add(Connected());
        var publisher = new StubPublisher(ContentPlatform.LinkedIn) { IsConfigured = false };
        var service = CreateService(store, publisher);

        var result = await service.PublishAsync(store.Candidates[0], Now, CancellationToken.None);

        Assert.Equal(PublishFailureReason.PlatformNotConfigured, result.FailureReason);
        Assert.Empty(publisher.AccessTokens);
    }

    [Fact]
    public async Task Rejected_credentials_are_reported_distinctly_from_an_outage()
    {
        var store = NewStore();
        store.Accounts.Add(Connected());
        var publisher = new StubPublisher(ContentPlatform.LinkedIn)
        {
            Throws = new SocialProviderAuthenticationException("rejected")
        };
        var service = CreateService(store, publisher);

        var result = await service.PublishAsync(store.Candidates[0], Now, CancellationToken.None);

        Assert.Equal(PublishFailureReason.CredentialsRejected, result.FailureReason);
    }

    [Fact]
    public async Task An_unreadable_provider_response_is_reported_as_invalid()
    {
        var store = NewStore();
        store.Accounts.Add(Connected());
        var publisher = new StubPublisher(ContentPlatform.LinkedIn)
        {
            Throws = new SocialProviderRequestException("bad body")
        };
        var service = CreateService(store, publisher);

        var result = await service.PublishAsync(store.Candidates[0], Now, CancellationToken.None);

        Assert.Equal(PublishFailureReason.ProviderResponseInvalid, result.FailureReason);
    }

    /// <summary>
    /// The platform accepted the content, so the outcome is known to be
    /// published. The post is deliberately left alone rather than being updated
    /// by a worker that no longer owns the claim.
    /// </summary>
    [Fact]
    public async Task A_lost_claim_after_a_successful_publish_does_not_write_to_the_post()
    {
        var store = NewStore(markPublishedSucceeds: false);
        store.Accounts.Add(Connected());
        var service = CreateService(store, new StubPublisher(ContentPlatform.LinkedIn));

        var result = await service.PublishAsync(store.Candidates[0], Now, CancellationToken.None);

        Assert.Equal(PublishOutcome.Failed, result.Outcome);
        Assert.Equal(PublishFailureReason.PublishClaimLost, result.FailureReason);
        Assert.Equal("external-1", result.ExternalPostId);
        Assert.Empty(store.Failures);
    }

    [Fact]
    public async Task A_lost_claim_on_failure_is_reported_rather_than_silently_dropped()
    {
        var store = NewStore(recordFailureSucceeds: false);
        store.Accounts.Add(Connected());
        var publisher = new StubPublisher(ContentPlatform.LinkedIn)
        {
            Throws = new SocialProviderUnavailableException("down")
        };
        var service = CreateService(store, publisher);

        var result = await service.PublishAsync(store.Candidates[0], Now, CancellationToken.None);

        Assert.Equal(PublishFailureReason.PublishClaimLost, result.FailureReason);
    }

    [Fact]
    public async Task The_published_text_combines_content_call_to_action_and_hashtags()
    {
        var store = NewStore();
        store.Payloads[0] = store.Payloads[0] with
        {
            Content = "Main content",
            CallToAction = "Read more",
            Hashtags = ["AI", "#Growth"]
        };
        store.Accounts.Add(Connected());
        var publisher = new StubPublisher(ContentPlatform.LinkedIn);
        var service = CreateService(store, publisher);

        await service.PublishAsync(store.Candidates[0], Now, CancellationToken.None);

        Assert.Equal(
            "Main content\n\nRead more\n\n#AI #Growth",
            Assert.Single(publisher.Requests).Text);
        Assert.Equal(PlatformAccountId, Assert.Single(publisher.Requests).PlatformAccountId);
    }

    private static ScheduledPostPublishService CreateService(
        FakeScheduledPostStore store,
        ISocialPlatformPublisher? publisher,
        ISocialCredentialProtector? protector = null)
    {
        ISocialPlatformPublisher[] publishers =
            publisher is null ? [] : [publisher];

        return new ScheduledPostPublishService(
            store,
            new SocialPlatformPublisherResolver(publishers),
            protector ?? new StubProtector(),
            NullLogger<ScheduledPostPublishService>.Instance);
    }

    private static ScheduledPostCandidate Due() =>
        new(
            Guid.NewGuid(),
            WorkspaceId,
            Guid.NewGuid(),
            ContentPlatform.LinkedIn,
            ContentPostStatus.Scheduled,
            Now.AddMinutes(-5),
            Now);

    private static Guid WorkspaceId { get; } = Guid.Parse("11111111-1111-1111-1111-111111111111");

    private static PublishAccountCandidate Connected(
        Guid? workspaceId = null,
        string platformAccountId = PlatformAccountId,
        SocialAccountStatus status = SocialAccountStatus.Connected,
        string? accessTokenEncrypted = "encrypted",
        DateTime? tokenExpiresAt = null) =>
        new(
            Guid.NewGuid(),
            workspaceId ?? WorkspaceId,
            ContentPlatform.LinkedIn,
            platformAccountId,
            status,
            accessTokenEncrypted,
            tokenExpiresAt);

    /// <summary>
    /// A store seeded with a single scheduled post for <see cref="WorkspaceId"/>.
    /// </summary>
    private static FakeScheduledPostStore NewStore(
        bool markPublishedSucceeds = true,
        bool recordFailureSucceeds = true)
    {
        var candidate = Due();
        return new FakeScheduledPostStore
        {
            Candidates = [candidate],
            MarkPublishedSucceeds = markPublishedSucceeds,
            RecordFailureSucceeds = recordFailureSucceeds,
            Payloads =
            [
                new ScheduledPostPayload(
                    candidate.ContentPostId,
                    WorkspaceId,
                    ContentPlatform.LinkedIn,
                    ContentPostStatus.Scheduled,
                    "Main content",
                    string.Empty,
                    ["#AI"])
            ]
        };
    }

    private sealed record PublishedMark(
        Guid PostId,
        DateTime ClaimedAt,
        DateTime PublishedAt,
        string? ExternalPostId);

    private sealed record FailureMark(Guid PostId, PublishFailureReason Reason);

    private sealed class StubPublisher(ContentPlatform platform) : ISocialPlatformPublisher
    {
        public ContentPlatform Platform { get; } = platform;

        public bool IsConfigured { get; init; } = true;

        public Exception? Throws { get; init; }

        public List<string> AccessTokens { get; } = [];

        public List<SocialPublishRequest> Requests { get; } = [];

        public Task<SocialPublishResult> PublishAsync(
            string accessToken,
            SocialPublishRequest request,
            CancellationToken cancellationToken)
        {
            AccessTokens.Add(accessToken);
            Requests.Add(request);

            return Throws is not null
                ? throw Throws
                : Task.FromResult(new SocialPublishResult("external-1", null));
        }
    }

    private sealed class StubProtector : ISocialCredentialProtector
    {
        public Exception? AccessTokenFailure { get; init; }

        public string ProtectAccessToken(string accessToken) => accessToken;

        public string ProtectRefreshToken(string refreshToken) => refreshToken;

        public string UnprotectAccessToken(string protectedAccessToken) =>
            AccessTokenFailure is not null
                ? throw AccessTokenFailure
                : "decrypted-token";

        public string UnprotectRefreshToken(string protectedRefreshToken) => "decrypted-refresh";
    }

    private sealed class FakeScheduledPostStore : IScheduledPostStore
    {
        public List<ScheduledPostCandidate> Candidates { get; init; } = [];

        public List<ScheduledPostPayload> Payloads { get; init; } = [];

        public List<PublishAccountCandidate> Accounts { get; } = [];

        public List<PublishedMark> PublishedMarks { get; } = [];

        public List<FailureMark> Failures { get; } = [];

        public bool MarkPublishedSucceeds { get; init; } = true;

        public bool RecordFailureSucceeds { get; init; } = true;
        public Task<IReadOnlyList<ScheduledPostCandidate>> FindDueAsync(
            DateTime now,
            int limit,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<ScheduledPostCandidate>>(Candidates);

        public Task<bool> TryClaimAsync(
            Guid contentPostId,
            DateTime now,
            TimeSpan claimExpiry,
            CancellationToken cancellationToken) =>
            Task.FromResult(true);

        public Task<bool> TryClaimForImmediatePublishAsync(
            Guid contentPostId,
            Guid workspaceId,
            DateTime now,
            TimeSpan claimExpiry,
            CancellationToken cancellationToken) =>
            Task.FromResult(false);

        public Task<bool> TryCancelScheduleAsync(
            Guid contentPostId,
            Guid workspaceId,
            DateTime now,
            TimeSpan claimExpiry,
            CancellationToken cancellationToken) =>
            Task.FromResult(false);

        public Task<bool> TryScheduleAsync(
            Guid contentPostId,
            Guid workspaceId,
            DateTime scheduledAt,
            DateTime now,
            TimeSpan claimExpiry,
            CancellationToken cancellationToken) =>
            Task.FromResult(false);

        public Task<ScheduledPostPayload?> GetPublishPayloadAsync(
            Guid contentPostId,
            CancellationToken cancellationToken) =>
            Task.FromResult(
                Payloads.SingleOrDefault(payload => payload.ContentPostId == contentPostId));

        public Task<IReadOnlyList<PublishAccountCandidate>> FindPublishAccountsAsync(
            Guid workspaceId,
            ContentPlatform platform,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<PublishAccountCandidate>>(
                Accounts
                    .Where(account =>
                        account.WorkspaceId == workspaceId &&
                        account.Platform == platform)
                    .ToList());

        public Task<bool> TryMarkPublishedAsync(
            Guid contentPostId,
            DateTime claimedAt,
            DateTime publishedAt,
            string? externalPostId,
            CancellationToken cancellationToken)
        {
            if (MarkPublishedSucceeds)
            {
                PublishedMarks.Add(
                    new PublishedMark(contentPostId, claimedAt, publishedAt, externalPostId));
            }

            return Task.FromResult(MarkPublishedSucceeds);
        }

        public Task<bool> TryRecordPublishFailureAsync(
            Guid contentPostId,
            DateTime claimedAt,
            PublishFailureReason reason,
            DateTime failedAt,
            CancellationToken cancellationToken)
        {
            if (RecordFailureSucceeds)
            {
                Failures.Add(new FailureMark(contentPostId, reason));
            }

            return Task.FromResult(RecordFailureSucceeds);
        }
    }
}
