using Api.Application.Publishing;
using Api.Application.Scheduling;
using Api.Configuration;
using Api.Domain.Entities;
using Api.Domain.Enums;
using Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Api.Tests;

public sealed class ScheduledPostClaimPolicyTests
{
    private static readonly DateTime Now = new(2026, 6, 1, 12, 0, 0, DateTimeKind.Utc);
    private static readonly TimeSpan Expiry = TimeSpan.FromMinutes(15);

    [Fact]
    public void A_scheduled_post_in_the_past_is_due()
    {
        var candidate = Candidate(scheduledAt: Now.AddMinutes(-1));

        Assert.True(ScheduledPostClaimPolicy.IsDue(candidate, Now));
    }

    [Fact]
    public void A_post_scheduled_exactly_now_is_due()
    {
        Assert.True(ScheduledPostClaimPolicy.IsDue(Candidate(scheduledAt: Now), Now));
    }

    [Fact]
    public void A_future_post_is_not_due()
    {
        Assert.False(ScheduledPostClaimPolicy.IsDue(
            Candidate(scheduledAt: Now.AddMinutes(1)),
            Now));
    }

    [Fact]
    public void A_post_without_a_scheduled_time_is_never_due()
    {
        Assert.False(ScheduledPostClaimPolicy.IsDue(Candidate(scheduledAt: null), Now));
    }

    [Theory]
    [InlineData(ContentPostStatus.Draft)]
    [InlineData(ContentPostStatus.Approved)]
    [InlineData(ContentPostStatus.Rejected)]
    [InlineData(ContentPostStatus.Published)]
    public void Only_scheduled_posts_are_due(ContentPostStatus status)
    {
        var candidate = Candidate(scheduledAt: Now.AddMinutes(-10), status: status);

        Assert.False(ScheduledPostClaimPolicy.IsDue(candidate, Now));
    }

    [Fact]
    public void An_unclaimed_post_can_be_claimed()
    {
        Assert.True(ScheduledPostClaimPolicy.CanBeClaimed(
            Candidate(claimedAt: null),
            Now,
            Expiry));
    }

    [Fact]
    public void A_fresh_claim_blocks_a_second_claim()
    {
        Assert.False(ScheduledPostClaimPolicy.CanBeClaimed(
            Candidate(claimedAt: Now.AddMinutes(-2)),
            Now,
            Expiry));
    }

    [Fact]
    public void An_expired_claim_can_be_retaken()
    {
        Assert.True(ScheduledPostClaimPolicy.CanBeClaimed(
            Candidate(claimedAt: Now.AddMinutes(-30)),
            Now,
            Expiry));
    }

    [Fact]
    public void A_null_claim_expiry_falls_back_to_fifteen_minutes() =>
        Assert.Equal(
            TimeSpan.FromMinutes(15),
            ScheduledPostClaimPolicy.ResolveClaimExpiry(null));

    [Fact]
    public void A_zero_claim_expiry_falls_back_to_fifteen_minutes() =>
        Assert.Equal(
            TimeSpan.FromMinutes(15),
            ScheduledPostClaimPolicy.ResolveClaimExpiry(TimeSpan.Zero));

    [Fact]
    public void A_negative_claim_expiry_falls_back_to_fifteen_minutes() =>
        Assert.Equal(
            TimeSpan.FromMinutes(15),
            ScheduledPostClaimPolicy.ResolveClaimExpiry(TimeSpan.FromMinutes(-5)));

    [Fact]
    public void A_configured_claim_expiry_is_honoured() =>
        Assert.Equal(
            TimeSpan.FromMinutes(45),
            ScheduledPostClaimPolicy.ResolveClaimExpiry(TimeSpan.FromMinutes(45)));

    private static ScheduledPostCandidate Candidate(
        DateTime? scheduledAt = null,
        DateTime? claimedAt = null,
        ContentPostStatus status = ContentPostStatus.Scheduled) =>
        new(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            ContentPlatform.LinkedIn,
            status,
            scheduledAt,
            claimedAt);
}

public sealed class ScheduledPostDbStoreTests
{
    [Fact]
    public async Task Find_due_returns_only_scheduled_posts_whose_time_has_passed()
    {
        var now = DateTime.UtcNow;
        await using var dbContext = CreateDbContext();
        var dueId = Add(dbContext, ContentPostStatus.Scheduled, now.AddMinutes(-1));
        Add(dbContext, ContentPostStatus.Scheduled, now.AddHours(2));
        Add(dbContext, ContentPostStatus.Approved, now.AddMinutes(-1));
        Add(dbContext, ContentPostStatus.Published, now.AddMinutes(-1));
        Add(dbContext, ContentPostStatus.Rejected, now.AddMinutes(-1));
        Add(dbContext, ContentPostStatus.Draft, now.AddMinutes(-1));
        var store = new ScheduledPostDbStore(dbContext);

        var due = await store.FindDueAsync(now, 50, CancellationToken.None);

        Assert.Equal([dueId], due.Select(candidate => candidate.ContentPostId).ToArray());
    }

    [Fact]
    public async Task Find_due_ignores_scheduled_posts_without_a_scheduled_time()
    {
        var now = DateTime.UtcNow;
        await using var dbContext = CreateDbContext();
        var post = Add(dbContext, ContentPostStatus.Scheduled, now.AddMinutes(-1));
        dbContext.ContentPosts.Single().ScheduledAt = null;
        await dbContext.SaveChangesAsync();
        var store = new ScheduledPostDbStore(dbContext);

        var due = await store.FindDueAsync(now, 50, CancellationToken.None);

        Assert.Empty(due);
    }

    [Fact]
    public async Task Find_due_returns_the_earliest_posts_first_and_honours_the_batch_size()
    {
        var now = DateTime.UtcNow;
        await using var dbContext = CreateDbContext();
        var latest = Add(dbContext, ContentPostStatus.Scheduled, now.AddMinutes(-1));
        var earliest = Add(dbContext, ContentPostStatus.Scheduled, now.AddMinutes(-30));
        var middle = Add(dbContext, ContentPostStatus.Scheduled, now.AddMinutes(-10));
        var store = new ScheduledPostDbStore(dbContext);

        var due = await store.FindDueAsync(now, 2, CancellationToken.None);

        Assert.Equal(
            [earliest, middle],
            due.Select(candidate => candidate.ContentPostId).ToArray());
        Assert.DoesNotContain(latest, due.Select(candidate => candidate.ContentPostId));
    }

    [Fact]
    public async Task Find_due_carries_the_workspace_and_claim_state()
    {
        var now = DateTime.UtcNow;
        await using var dbContext = CreateDbContext();
        var workspaceId = Guid.NewGuid();
        var contentIdeaId = Guid.NewGuid();
        Add(dbContext, ContentPostStatus.Scheduled, now.AddMinutes(-1), workspaceId, contentIdeaId);
        dbContext.ContentPosts.Single().PublishClaimedAt = now.AddMinutes(-5);
        await dbContext.SaveChangesAsync();
        var store = new ScheduledPostDbStore(dbContext);

        var due = await store.FindDueAsync(now, 50, CancellationToken.None);

        var candidate = Assert.Single(due);
        Assert.Equal(workspaceId, candidate.WorkspaceId);
        Assert.Equal(contentIdeaId, candidate.ContentIdeaId);
        Assert.Equal(now.AddMinutes(-5), candidate.PublishClaimedAt);
    }

    [Fact]
    public async Task Find_due_spans_workspaces_because_the_worker_is_system_scoped()
    {
        var now = DateTime.UtcNow;
        await using var dbContext = CreateDbContext();
        var firstWorkspace = Guid.NewGuid();
        var secondWorkspace = Guid.NewGuid();
        var firstId = Add(dbContext, ContentPostStatus.Scheduled, now.AddMinutes(-5), firstWorkspace);
        var secondId = Add(dbContext, ContentPostStatus.Scheduled, now.AddMinutes(-5), secondWorkspace);
        var store = new ScheduledPostDbStore(dbContext);

        var due = await store.FindDueAsync(now, 50, CancellationToken.None);

        Assert.Equal(
            new[] { firstId, secondId }.Order(),
            due.Select(candidate => candidate.ContentPostId).Order());
    }

    [Fact]
    public async Task The_publish_payload_carries_the_text_parts()
    {
        var now = DateTime.UtcNow;
        await using var dbContext = CreateDbContext();
        var workspaceId = Guid.NewGuid();
        var postId = Add(dbContext, ContentPostStatus.Scheduled, now.AddMinutes(-1), workspaceId);
        var post = dbContext.ContentPosts.Single();
        post.CallToAction = "Read more";
        post.Hashtags = ["#AI", "#Growth"];
        await dbContext.SaveChangesAsync();
        var store = new ScheduledPostDbStore(dbContext);

        var payload = await store.GetPublishPayloadAsync(postId, CancellationToken.None);

        Assert.NotNull(payload);
        Assert.Equal(workspaceId, payload!.WorkspaceId);
        Assert.Equal(ContentPlatform.LinkedIn, payload.Platform);
        Assert.Equal("Content", payload.Content);
        Assert.Equal("Read more", payload.CallToAction);
        Assert.Equal(["#AI", "#Growth"], payload.Hashtags);
    }

    [Fact]
    public async Task Publish_accounts_are_scoped_to_the_posts_workspace_and_platform()
    {
        await using var dbContext = CreateDbContext();
        var workspaceId = Guid.NewGuid();
        var otherWorkspaceId = Guid.NewGuid();
        var matching = AddAccount(dbContext, workspaceId, ContentPlatform.LinkedIn, SocialAccountStatus.Connected);
        AddAccount(dbContext, otherWorkspaceId, ContentPlatform.LinkedIn, SocialAccountStatus.Connected);
        AddAccount(dbContext, workspaceId, ContentPlatform.X, SocialAccountStatus.Connected);
        var store = new ScheduledPostDbStore(dbContext);

        var accounts = await store.FindPublishAccountsAsync(
            workspaceId,
            ContentPlatform.LinkedIn,
            CancellationToken.None);

        var account = Assert.Single(accounts);
        Assert.Equal(matching, account.SocialAccountId);
    }

    [Fact]
    public async Task The_account_query_returns_every_account_so_ambiguity_can_be_detected()
    {
        await using var dbContext = CreateDbContext();
        var workspaceId = Guid.NewGuid();
        AddAccount(dbContext, workspaceId, ContentPlatform.LinkedIn, SocialAccountStatus.Connected);
        AddAccount(dbContext, workspaceId, ContentPlatform.LinkedIn, SocialAccountStatus.Disconnected);
        var store = new ScheduledPostDbStore(dbContext);

        var accounts = await store.FindPublishAccountsAsync(
            workspaceId,
            ContentPlatform.LinkedIn,
            CancellationToken.None);

        Assert.Equal(2, accounts.Count);
    }

    /// <summary>
    /// Records a real coverage gap instead of pretending it does not exist.
    /// <para>
    /// Every write path on the production store uses <c>ExecuteUpdateAsync</c>
    /// for its conditional write, and the EF InMemory provider does not support
    /// it at all: it throws <c>InvalidOperationException</c>. So
    /// <see cref="ScheduledPostDbStore.TryClaimAsync"/>,
    /// <see cref="ScheduledPostDbStore.TryMarkPublishedAsync"/> and
    /// <see cref="ScheduledPostDbStore.TryRecordPublishFailureAsync"/> have never
    /// executed in this suite, and cannot until a relational integration test
    /// exists. The fake-store and publish-service tests cover the decisions made
    /// around a claim, not the atomicity of the write that takes it.
    /// </para>
    /// <para>
    /// Two workers really cannot both win a claim because the conditional update
    /// is a single atomic SQL statement. That is a property of the provider, not
    /// something this test can assert, so it is left to PostgreSQL.
    /// </para>
    /// </summary>
    [Fact]
    public async Task The_in_memory_provider_cannot_execute_the_conditional_write_paths()
    {
        await using var dbContext = CreateDbContext();
        var postId = Add(dbContext, ContentPostStatus.Scheduled, DateTime.UtcNow.AddMinutes(-1));
        var store = new ScheduledPostDbStore(dbContext);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            store.TryClaimAsync(
                postId,
                DateTime.UtcNow,
                TimeSpan.FromMinutes(15),
                CancellationToken.None));
    }

    private static AppDbContext CreateDbContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"store-{Guid.NewGuid()}")
            .Options);

    private static Guid AddAccount(
        AppDbContext dbContext,
        Guid workspaceId,
        ContentPlatform platform,
        SocialAccountStatus status)
    {
        var account = new SocialAccount
        {
            Id = Guid.NewGuid(),
            WorkspaceId = workspaceId,
            Platform = platform,
            PlatformAccountId = "acct",
            Status = status,
            AccessTokenEncrypted = "encrypted",
            TokenExpiresAt = DateTime.UtcNow.AddHours(1),
            ConnectedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        dbContext.SocialAccounts.Add(account);
        dbContext.SaveChanges();
        return account.Id;
    }

    private static Guid Add(
        AppDbContext dbContext,
        ContentPostStatus status,
        DateTime? scheduledAt,
        Guid? workspaceId = null,
        Guid? contentIdeaId = null)
    {
        var post = new ContentPost
        {
            Id = Guid.NewGuid(),
            WorkspaceId = workspaceId ?? Guid.NewGuid(),
            ContentIdeaId = contentIdeaId ?? Guid.NewGuid(),
            Platform = ContentPlatform.LinkedIn,
            Status = status,
            Content = "Content",
            Hook = "Hook",
            CallToAction = string.Empty,
            Hashtags = ["#AI"],
            ContentType = ContentType.Insight,
            StorytellingStructure = "Structure",
            ScheduledAt = scheduledAt,
            CreatedAt = DateTime.UtcNow.AddDays(-1),
            UpdatedAt = DateTime.UtcNow.AddDays(-1)
        };
        dbContext.ContentPosts.Add(post);
        dbContext.SaveChanges();
        return post.Id;
    }
}

public sealed class ScheduledPostProcessorTests
{
    [Fact]
    public async Task Due_scheduled_posts_are_claimed()
    {
        var store = new FakeScheduledPostStore();
        store.Add(Due());
        var processor = CreateProcessor(store);

        var result = await processor.RunOnceAsync(CancellationToken.None);

        Assert.Equal(1, result.DueFound);
        Assert.Equal(1, result.Claimed);
        Assert.Equal(0, result.SkippedAlreadyClaimed);
        Assert.Equal([store.Candidates[0].ContentPostId], store.ClaimAttempts);
    }

    [Fact]
    public async Task Future_scheduled_posts_are_ignored()
    {
        var store = new FakeScheduledPostStore();
        store.Add(Due(scheduledAt: DateTime.UtcNow.AddHours(4)));
        var processor = CreateProcessor(store);

        var result = await processor.RunOnceAsync(CancellationToken.None);

        Assert.Equal(0, result.DueFound);
        Assert.Equal(0, result.Claimed);
        Assert.Empty(store.ClaimAttempts);
    }

    [Fact]
    public async Task Non_scheduled_posts_are_ignored_even_when_their_time_has_passed()
    {
        var store = new FakeScheduledPostStore();
        store.Add(Due(status: ContentPostStatus.Draft));
        store.Add(Due(status: ContentPostStatus.Approved));
        store.Add(Due(status: ContentPostStatus.Published));
        store.Add(Due(status: ContentPostStatus.Rejected));
        var processor = CreateProcessor(store);

        var result = await processor.RunOnceAsync(CancellationToken.None);

        Assert.Equal(0, result.DueFound);
        Assert.Empty(store.ClaimAttempts);
    }

    [Fact]
    public async Task A_fresh_claim_is_not_retaken()
    {
        var store = new FakeScheduledPostStore();
        store.Add(Due(claimedAt: DateTime.UtcNow.AddMinutes(-1)));
        var processor = CreateProcessor(store);

        var result = await processor.RunOnceAsync(CancellationToken.None);

        Assert.Equal(1, result.DueFound);
        Assert.Equal(0, result.Claimed);
        Assert.Equal(1, result.SkippedAlreadyClaimed);
        Assert.Empty(store.ClaimAttempts);
    }

    [Fact]
    public async Task A_concurrent_worker_that_loses_the_claim_does_not_process_the_post()
    {
        var store = new FakeScheduledPostStore { ClaimSucceeds = false };
        store.Add(Due());
        var processor = CreateProcessor(store);

        var result = await processor.RunOnceAsync(CancellationToken.None);

        Assert.Equal(1, result.DueFound);
        Assert.Equal(0, result.Claimed);
        Assert.Equal(1, result.SkippedAlreadyClaimed);
    }

    [Fact]
    public async Task Only_one_of_two_workers_winning_the_same_post_is_recorded_as_claimed()
    {
        var store = new FakeScheduledPostStore();
        var candidate = Due();
        store.Add(candidate);
        var first = CreateProcessor(store);
        var second = CreateProcessor(store);

        var firstResult = await first.RunOnceAsync(CancellationToken.None);
        var secondResult = await second.RunOnceAsync(CancellationToken.None);

        Assert.Equal(1, firstResult.Claimed);
        Assert.Equal(0, secondResult.Claimed);
        Assert.Single(store.ClaimAttempts);
    }

    [Fact]
    public async Task A_claimed_post_is_published_and_counted()
    {
        var store = new FakeScheduledPostStore();
        store.Add(Due());
        var publishService = new FakeScheduledPostPublishService();
        var processor = CreateProcessor(store, publishService);

        var result = await processor.RunOnceAsync(CancellationToken.None);

        Assert.Equal(1, result.Claimed);
        Assert.Equal(1, result.Published);
        Assert.Equal(0, result.Failed);
        Assert.Equal([store.Candidates[0].ContentPostId], publishService.Attempts);
    }

    [Fact]
    public async Task A_failed_publish_is_counted_without_publishing()
    {
        var store = new FakeScheduledPostStore();
        store.Add(Due());
        var publishService = new FakeScheduledPostPublishService { Outcome = PublishOutcome.Failed };
        var processor = CreateProcessor(store, publishService);

        var result = await processor.RunOnceAsync(CancellationToken.None);

        Assert.Equal(1, result.Claimed);
        Assert.Equal(0, result.Published);
        Assert.Equal(1, result.Failed);
    }

    [Fact]
    public async Task A_publish_uses_the_claim_this_worker_just_took()
    {
        var store = new FakeScheduledPostStore();
        store.Add(Due());
        var publishService = new FakeScheduledPostPublishService();
        var processor = CreateProcessor(store, publishService);

        await processor.RunOnceAsync(CancellationToken.None);

        var claimTakenByThisWorker = store.Candidates[0].PublishClaimedAt;
        Assert.NotNull(claimTakenByThisWorker);
        Assert.Equal(claimTakenByThisWorker, Assert.Single(publishService.ClaimTimestamps));
    }

    [Fact]
    public async Task A_post_that_throws_does_not_abandon_the_rest_of_the_batch()
    {
        var store = new FakeScheduledPostStore();
        store.Add(Due());
        store.Add(Due());
        var publishService = new FakeScheduledPostPublishService { Throws = true };
        var processor = CreateProcessor(store, publishService);

        var result = await processor.RunOnceAsync(CancellationToken.None);

        Assert.Equal(2, result.Claimed);
        Assert.Equal(2, result.Failed);
        Assert.Equal(2, publishService.Attempts.Count);
    }

    [Fact]
    public void Every_status_changing_store_method_requires_a_claim()
    {
        // A worker may only finalise a post it still owns, so every method that
        // changes a post's status must take the exact claim it was given, and
        // none may accept a status to write.
        var writers = typeof(IScheduledPostStore)
            .GetMethods()
            .Where(method => method.Name is "TryMarkPublishedAsync" or "TryRecordPublishFailureAsync")
            .ToArray();

        Assert.Equal(2, writers.Length);
        Assert.All(
            writers,
            method =>
            {
                Assert.Contains(
                    method.GetParameters(),
                    parameter => parameter.ParameterType == typeof(DateTime));
                Assert.DoesNotContain(
                    method.GetParameters(),
                    parameter => parameter.ParameterType == typeof(ContentPostStatus));
            });
    }

    [Fact]
    public async Task Posts_from_different_workspaces_are_claimed_independently()
    {
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        var store = new FakeScheduledPostStore();
        store.Add(Due(workspaceId: first));
        store.Add(Due(workspaceId: second));
        var processor = CreateProcessor(store, new FakeScheduledPostPublishService());

        var result = await processor.RunOnceAsync(CancellationToken.None);

        Assert.Equal(2, result.Claimed);
        Assert.Equal(2, result.Published);
        Assert.Equal(
            new[] { first, second }.Order(),
            store.ClaimedWorkspaceIds.Order());
    }

    [Fact]
    public async Task The_batch_size_bounds_a_single_scan()
    {
        var store = new FakeScheduledPostStore();
        for (var index = 0; index < 10; index++)
        {
            store.Add(Due());
        }
        var processor = CreateProcessor(
            store,
            new FakeScheduledPostPublishService(),
            batchSize: 3);

        var result = await processor.RunOnceAsync(CancellationToken.None);

        Assert.Equal(3, result.DueFound);
        Assert.Equal(3, result.Claimed);
    }

    [Fact]
    public async Task An_empty_queue_is_a_no_op()
    {
        var processor = CreateProcessor(
            new FakeScheduledPostStore(),
            new FakeScheduledPostPublishService());

        var result = await processor.RunOnceAsync(CancellationToken.None);

        Assert.Equal(new ScheduledPostRunResult(0, 0, 0), result);
    }

    /// <remarks>
    /// Tests that only exercise claiming can omit the publish service and get a
    /// successful publisher, so their assertions stay about the claim.
    /// </remarks>
    private static ScheduledPostProcessor CreateProcessor(
        IScheduledPostStore store,
        IScheduledPostPublishService? publishService = null,
        int batchSize = 25) =>
        new(
            store,
            publishService ?? new FakeScheduledPostPublishService(),
            Options.Create(new SchedulingOptions
            {
                BatchSize = batchSize,
                ClaimExpiryMinutes = 15
            }),
            NullLogger<ScheduledPostProcessor>.Instance);

    private static ScheduledPostCandidate Due(
        DateTime? scheduledAt = null,
        DateTime? claimedAt = null,
        ContentPostStatus status = ContentPostStatus.Scheduled,
        Guid? workspaceId = null) =>
        new(
            Guid.NewGuid(),
            workspaceId ?? Guid.NewGuid(),
            Guid.NewGuid(),
            ContentPlatform.LinkedIn,
            status,
            scheduledAt ?? DateTime.UtcNow.AddMinutes(-5),
            claimedAt);

    private sealed class FakeScheduledPostPublishService : IScheduledPostPublishService
    {
        public List<Guid> Attempts { get; } = [];

        public List<DateTime> ClaimTimestamps { get; } = [];

        public PublishOutcome Outcome { get; init; } = PublishOutcome.Published;

        public bool Throws { get; init; }

        public Task<PublishAttemptResult> PublishAsync(
            ScheduledPostCandidate candidate,
            DateTime claimedAt,
            CancellationToken cancellationToken)
        {
            Attempts.Add(candidate.ContentPostId);
            ClaimTimestamps.Add(claimedAt);

            if (Throws)
            {
                throw new InvalidOperationException("Publish blew up.");
            }

            return Task.FromResult(new PublishAttemptResult(
                candidate.ContentPostId,
                Outcome,
                Outcome == PublishOutcome.Failed
                    ? PublishFailureReason.ProviderUnavailable
                    : null,
                Outcome == PublishOutcome.Published ? "external-1" : null));
        }
    }

    private sealed class FakeScheduledPostStore : IScheduledPostStore
    {
        public List<ScheduledPostCandidate> Candidates { get; } = [];

        public List<Guid> ClaimAttempts { get; } = [];

        public List<Guid> ClaimedWorkspaceIds { get; } = [];

        public bool ClaimSucceeds { get; init; } = true;

        public void Add(ScheduledPostCandidate candidate) => Candidates.Add(candidate);

        public Task<IReadOnlyList<ScheduledPostCandidate>> FindDueAsync(
            DateTime now,
            int limit,
            CancellationToken cancellationToken)
        {
            var due = Candidates
                .Where(candidate => ScheduledPostClaimPolicy.IsDue(candidate, now))
                .Take(limit)
                .ToList();
            return Task.FromResult<IReadOnlyList<ScheduledPostCandidate>>(due);
        }

        public Task<bool> TryClaimAsync(
            Guid contentPostId,
            DateTime now,
            TimeSpan claimExpiry,
            CancellationToken cancellationToken)
        {
            ClaimAttempts.Add(contentPostId);
            if (!ClaimSucceeds)
            {
                return Task.FromResult(false);
            }

            // Mirrors the production conditional update: a row is only written
            // while it is still unclaimed or holding an expired claim.
            var index = Candidates.FindIndex(item => item.ContentPostId == contentPostId);
            var candidate = Candidates[index];
            if (!ScheduledPostClaimPolicy.CanBeClaimed(candidate, now, claimExpiry))
            {
                return Task.FromResult(false);
            }

            Candidates[index] = candidate with { PublishClaimedAt = now };
            ClaimedWorkspaceIds.Add(candidate.WorkspaceId);
            return Task.FromResult(true);
        }

        public Task<ScheduledPostPayload?> GetPublishPayloadAsync(
            Guid contentPostId,
            CancellationToken cancellationToken) =>
            Task.FromResult<ScheduledPostPayload?>(null);

        public Task<IReadOnlyList<PublishAccountCandidate>> FindPublishAccountsAsync(
            Guid workspaceId,
            ContentPlatform platform,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<PublishAccountCandidate>>([]);

        public Task<bool> TryMarkPublishedAsync(
            Guid contentPostId,
            DateTime claimedAt,
            DateTime publishedAt,
            string? externalPostId,
            CancellationToken cancellationToken) =>
            Task.FromResult(true);

        public Task<bool> TryRecordPublishFailureAsync(
            Guid contentPostId,
            DateTime claimedAt,
            PublishFailureReason reason,
            DateTime failedAt,
            CancellationToken cancellationToken) =>
            Task.FromResult(true);
    }
}
