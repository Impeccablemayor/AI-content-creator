using System.Security.Cryptography;
using Api.Application.AnalyticsSync;
using Api.Application.Security;
using Api.Application.SocialAccounts;
using Api.Domain.Enums;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Api.Tests;

public sealed class PublishedPostMetricsSyncServiceTests
{
    private static readonly DateTime Now = new(2026, 6, 1, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task A_successful_read_stores_a_snapshot_for_the_post()
    {
        var store = NewStore();
        store.Accounts.Add(Connected());
        var provider = new StubMetricsProvider(ContentPlatform.X)
        {
            Returns = new SocialMetricsSnapshot(1_000, 12, 3, 4, 5, 6, 7)
        };
        var service = CreateService(store, provider);

        var result = await service.SyncAsync(store.Candidates[0], CancellationToken.None);

        Assert.Equal(MetricsSyncOutcome.Synced, result.Outcome);
        var snapshot = Assert.Single(store.Snapshots);
        Assert.Equal(WorkspaceId, snapshot.WorkspaceId);
        Assert.Equal(store.Candidates[0].ContentPostId, snapshot.ContentPostId);
        Assert.Equal(1_000, snapshot.Impressions);
        Assert.Equal(12, snapshot.Likes);
        Assert.Equal(3, snapshot.Comments);
        Assert.Equal(4, snapshot.Shares);
        Assert.Equal(5, snapshot.Clicks);
        Assert.Equal(6, snapshot.ProfileVisits);
        Assert.Equal(7, snapshot.FollowersGained);
        Assert.Equal("decrypted-token", Assert.Single(provider.AccessTokens));
    }

    [Fact]
    public async Task The_platform_post_id_stored_at_publish_time_is_the_one_that_is_measured()
    {
        var store = NewStore();
        store.Accounts.Add(Connected());
        var provider = new StubMetricsProvider(ContentPlatform.X);
        var service = CreateService(store, provider);

        await service.SyncAsync(store.Candidates[0], CancellationToken.None);

        Assert.Equal("external-1", Assert.Single(provider.ExternalPostIds));
    }

    /// <summary>
    /// A read that came back empty is a measurement. Dropping it would make
    /// "the platform reported nothing" indistinguishable from "never measured".
    /// </summary>
    [Fact]
    public async Task A_read_that_reports_nothing_is_still_recorded()
    {
        var store = NewStore();
        store.Accounts.Add(Connected());
        var provider = new StubMetricsProvider(ContentPlatform.X)
        {
            Returns = new SocialMetricsSnapshot()
        };
        var service = CreateService(store, provider);

        var result = await service.SyncAsync(store.Candidates[0], CancellationToken.None);

        Assert.Equal(MetricsSyncOutcome.Synced, result.Outcome);
        var snapshot = Assert.Single(store.Snapshots);
        Assert.Null(snapshot.Impressions);
        Assert.NotEqual(default, snapshot.RecordedAt);
    }

    /// <summary>
    /// The attempt timestamp is the only backoff, so it has to be written for
    /// every outcome or a post the platform cannot measure is retried on every
    /// poll forever.
    /// </summary>
    [Fact]
    public async Task A_failed_read_records_the_attempt_but_writes_no_snapshot()
    {
        var store = NewStore();
        store.Accounts.Add(Connected());
        var provider = new StubMetricsProvider(ContentPlatform.X)
        {
            Throws = new SocialProviderUnavailableException("down")
        };
        var service = CreateService(store, provider);

        var result = await service.SyncAsync(store.Candidates[0], CancellationToken.None);

        Assert.Equal(MetricsSyncOutcome.Failed, result.Outcome);
        Assert.Equal(MetricsSyncFailureReason.ProviderUnavailable, result.FailureReason);
        Assert.Equal(store.Candidates[0].ContentPostId, Assert.Single(store.Attempts));
        Assert.Empty(store.Snapshots);
    }

    [Fact]
    public async Task Rejected_credentials_are_reported_distinctly_from_an_outage()
    {
        var store = NewStore();
        store.Accounts.Add(Connected());
        var provider = new StubMetricsProvider(ContentPlatform.X)
        {
            Throws = new SocialProviderAuthenticationException("rejected")
        };
        var service = CreateService(store, provider);

        var result = await service.SyncAsync(store.Candidates[0], CancellationToken.None);

        Assert.Equal(MetricsSyncFailureReason.CredentialsRejected, result.FailureReason);
    }

    [Fact]
    public async Task An_unreadable_provider_response_is_reported_as_invalid()
    {
        var store = NewStore();
        store.Accounts.Add(Connected());
        var provider = new StubMetricsProvider(ContentPlatform.X)
        {
            Throws = new SocialProviderRequestException("bad body")
        };
        var service = CreateService(store, provider);

        var result = await service.SyncAsync(store.Candidates[0], CancellationToken.None);

        Assert.Equal(MetricsSyncFailureReason.ProviderResponseInvalid, result.FailureReason);
    }

    [Fact]
    public async Task A_workspace_with_no_social_account_fails_with_not_found()
    {
        var store = NewStore();
        var service = CreateService(store, new StubMetricsProvider(ContentPlatform.X));

        var result = await service.SyncAsync(store.Candidates[0], CancellationToken.None);

        Assert.Equal(MetricsSyncFailureReason.SocialAccountNotFound, result.FailureReason);
    }

    [Fact]
    public async Task A_disconnected_account_is_not_used()
    {
        var store = NewStore();
        store.Accounts.Add(Connected(status: SocialAccountStatus.Disconnected));
        var provider = new StubMetricsProvider(ContentPlatform.X);
        var service = CreateService(store, provider);

        var result = await service.SyncAsync(store.Candidates[0], CancellationToken.None);

        Assert.Equal(MetricsSyncFailureReason.SocialAccountNotConnected, result.FailureReason);
        Assert.Empty(provider.AccessTokens);
    }

    [Fact]
    public async Task An_expired_account_is_reported_as_expired_rather_than_not_connected()
    {
        var store = NewStore();
        store.Accounts.Add(Connected(status: SocialAccountStatus.Expired));
        var service = CreateService(store, new StubMetricsProvider(ContentPlatform.X));

        var result = await service.SyncAsync(store.Candidates[0], CancellationToken.None);

        Assert.Equal(MetricsSyncFailureReason.CredentialsExpired, result.FailureReason);
    }

    [Fact]
    public async Task A_connected_account_whose_token_has_expired_is_not_used()
    {
        var store = NewStore();
        store.Accounts.Add(Connected(tokenExpiresAt: DateTime.UtcNow.AddMinutes(-1)));
        var provider = new StubMetricsProvider(ContentPlatform.X);
        var service = CreateService(store, provider);

        var result = await service.SyncAsync(store.Candidates[0], CancellationToken.None);

        Assert.Equal(MetricsSyncFailureReason.CredentialsExpired, result.FailureReason);
        Assert.Empty(provider.AccessTokens);
    }

    /// <summary>
    /// Reading through an account the workspace never chose would attribute one
    /// member's reach to another's post.
    /// </summary>
    [Fact]
    public async Task Several_connected_accounts_are_ambiguous_rather_than_guessed()
    {
        var store = NewStore();
        store.Accounts.Add(Connected());
        store.Accounts.Add(Connected(socialAccountId: Guid.NewGuid()));
        var provider = new StubMetricsProvider(ContentPlatform.X);
        var service = CreateService(store, provider);

        var result = await service.SyncAsync(store.Candidates[0], CancellationToken.None);

        Assert.Equal(MetricsSyncFailureReason.SocialAccountAmbiguous, result.FailureReason);
        Assert.Empty(provider.AccessTokens);
    }

    [Fact]
    public async Task An_account_with_no_stored_token_fails_as_unavailable()
    {
        var store = NewStore();
        store.Accounts.Add(Connected(accessTokenEncrypted: null));
        var service = CreateService(store, new StubMetricsProvider(ContentPlatform.X));

        var result = await service.SyncAsync(store.Candidates[0], CancellationToken.None);

        Assert.Equal(MetricsSyncFailureReason.CredentialsUnavailable, result.FailureReason);
    }

    /// <summary>
    /// The isolation guarantee. A post must never be measured through an account
    /// belonging to a different workspace, even if a bug upstream returned one.
    /// </summary>
    [Fact]
    public async Task An_account_owned_by_another_workspace_is_refused()
    {
        var store = NewStore();
        store.Accounts.Add(Connected(workspaceId: Guid.NewGuid()));
        var provider = new StubMetricsProvider(ContentPlatform.X);
        var service = CreateService(store, provider);

        var result = await service.SyncAsync(store.Candidates[0], CancellationToken.None);

        Assert.Equal(MetricsSyncFailureReason.SocialAccountNotFound, result.FailureReason);
        Assert.Empty(provider.AccessTokens);
        Assert.Empty(store.Snapshots);
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
            AccessTokenFailure = new CryptographicException("key ring xyz is not available")
        };
        var service = CreateService(
            store,
            new StubMetricsProvider(ContentPlatform.X),
            protector);

        var result = await service.SyncAsync(store.Candidates[0], CancellationToken.None);

        Assert.Equal(MetricsSyncFailureReason.CredentialsUnavailable, result.FailureReason);
        Assert.DoesNotContain("key ring xyz", result.Detail!, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_platform_with_no_metrics_provider_is_skipped()
    {
        var store = NewStore();
        store.Accounts.Add(Connected());
        var service = CreateService(store, provider: null);

        var result = await service.SyncAsync(store.Candidates[0], CancellationToken.None);

        Assert.Equal(MetricsSyncOutcome.Skipped, result.Outcome);
        Assert.Equal(MetricsSyncFailureReason.PlatformNotSupported, result.FailureReason);
        Assert.Empty(store.Snapshots);
    }

    [Fact]
    public async Task A_platform_that_is_not_configured_is_skipped_before_any_request()
    {
        var store = NewStore();
        store.Accounts.Add(Connected());
        var provider = new StubMetricsProvider(ContentPlatform.X) { IsConfigured = false };
        var service = CreateService(store, provider);

        var result = await service.SyncAsync(store.Candidates[0], CancellationToken.None);

        Assert.Equal(MetricsSyncOutcome.Skipped, result.Outcome);
        Assert.Equal(MetricsSyncFailureReason.PlatformNotConfigured, result.FailureReason);
        Assert.Empty(provider.AccessTokens);
    }

    /// <summary>
    /// The post can stop being measurable between the scan and the read, for
    /// example if it was unpublished. Writing then would touch a row that is no
    /// longer eligible.
    /// </summary>
    [Fact]
    public async Task A_post_that_is_no_longer_eligible_is_not_measured()
    {
        var store = NewStore(recordAttemptSucceeds: false);
        store.Accounts.Add(Connected());
        var provider = new StubMetricsProvider(ContentPlatform.X);
        var service = CreateService(store, provider);

        var result = await service.SyncAsync(store.Candidates[0], CancellationToken.None);

        Assert.Equal(MetricsSyncOutcome.Skipped, result.Outcome);
        Assert.Empty(provider.AccessTokens);
        Assert.Empty(store.Snapshots);
    }

    private static PublishedPostMetricsSyncService CreateService(
        FakeAnalyticsSyncStore store,
        ISocialPlatformMetricsProvider? provider,
        ISocialCredentialProtector? protector = null)
    {
        ISocialPlatformMetricsProvider[] providers = provider is null ? [] : [provider];

        return new PublishedPostMetricsSyncService(
            store,
            new SocialPlatformMetricsProviderResolver(providers),
            protector ?? new StubProtector(),
            NullLogger<PublishedPostMetricsSyncService>.Instance);
    }

    private static Guid WorkspaceId { get; } = Guid.Parse("11111111-1111-1111-1111-111111111111");

    private static MetricsSyncAccountCandidate Connected(
        Guid? workspaceId = null,
        Guid? socialAccountId = null,
        SocialAccountStatus status = SocialAccountStatus.Connected,
        string? accessTokenEncrypted = "encrypted",
        DateTime? tokenExpiresAt = null) =>
        new(
            socialAccountId ?? Guid.NewGuid(),
            workspaceId ?? WorkspaceId,
            ContentPlatform.X,
            status,
            accessTokenEncrypted,
            tokenExpiresAt);

    /// <summary>
    /// A store seeded with a single published post for <see cref="WorkspaceId"/>.
    /// </summary>
    private static FakeAnalyticsSyncStore NewStore(bool recordAttemptSucceeds = true)
    {
        var candidate = new MetricsSyncCandidate(
            Guid.NewGuid(),
            WorkspaceId,
            ContentPlatform.X,
            "external-1",
            Now.AddHours(-2));

        return new FakeAnalyticsSyncStore
        {
            Candidates = [candidate],
            RecordAttemptSucceeds = recordAttemptSucceeds
        };
    }

    private sealed class StubMetricsProvider(ContentPlatform platform) : ISocialPlatformMetricsProvider
    {
        public ContentPlatform Platform { get; } = platform;

        public bool IsConfigured { get; init; } = true;

        public SocialMetricsSnapshot Returns { get; init; } = new(
            Impressions: 1,
            Likes: 2,
            Comments: 3,
            Shares: 4,
            Clicks: 5,
            ProfileVisits: 6,
            FollowersGained: 7);

        public Exception? Throws { get; init; }

        public List<string> AccessTokens { get; } = [];

        public List<string> ExternalPostIds { get; } = [];

        public Task<SocialMetricsSnapshot> FetchAsync(
            string accessToken,
            string externalPostId,
            CancellationToken cancellationToken)
        {
            AccessTokens.Add(accessToken);
            ExternalPostIds.Add(externalPostId);

            return Throws is not null
                ? throw Throws
                : Task.FromResult(Returns);
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

    private sealed class FakeAnalyticsSyncStore : IAnalyticsSyncStore
    {
        public IReadOnlyList<MetricsSyncCandidate> Candidates { get; init; } = [];

        public List<MetricsSyncAccountCandidate> Accounts { get; } = [];

        public List<Guid> Attempts { get; } = [];

        public List<ContentPostMetricSnapshot> Snapshots { get; } = [];

        public bool RecordAttemptSucceeds { get; init; } = true;

        public Task<IReadOnlyList<MetricsSyncCandidate>> FindDueAsync(
            DateTime dueBefore,
            int limit,
            CancellationToken cancellationToken) =>
            Task.FromResult(Candidates);

        public Task<IReadOnlyList<MetricsSyncAccountCandidate>> FindAccountsAsync(
            Guid workspaceId,
            ContentPlatform platform,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<MetricsSyncAccountCandidate>>(Accounts);

        public Task<bool> TryRecordAttemptAsync(
            Guid contentPostId,
            DateTime attemptedAt,
            CancellationToken cancellationToken)
        {
            if (RecordAttemptSucceeds)
            {
                Attempts.Add(contentPostId);
            }

            return Task.FromResult(RecordAttemptSucceeds);
        }

        public Task AddSnapshotAsync(
            ContentPostMetricSnapshot snapshot,
            CancellationToken cancellationToken)
        {
            Snapshots.Add(snapshot);
            return Task.CompletedTask;
        }
    }
}
