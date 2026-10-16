using System.Security.Claims;
using Api.Application.AI;
using Api.Application.AI.Prompts;
using Api.Application.Authentication;
using Api.Application.ContentEvaluation;
using Api.Application.ContentIdeas;
using Api.Application.ContentPosts;
using Api.Application.Scheduling;
using Api.Application.Services;
using Api.Configuration;
using Api.Contracts.ContentPost;
using Api.Domain.Entities;
using Api.Domain.Enums;
using Api.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Api.Tests;

public sealed class ContentPostSchedulingTests
{
    private static readonly DateTime OldTimestamp = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Scheduling_an_approved_post_delegates_the_write_to_the_store()
    {
        var workspaceId = Guid.NewGuid();
        await using var dbContext = CreateDbContext();
        var post = SeedPost(dbContext, workspaceId, ContentPostStatus.Approved);
        var store = new StubScheduledPostStore();
        var service = CreateService(dbContext, workspaceId, store);
        var scheduledAt = DateTimeOffset.UtcNow.AddHours(3);

        var result = await service.ScheduleAsync(
            post.Id,
            new ScheduleContentPostRequest { ScheduledAt = scheduledAt });

        Assert.NotNull(result);
        var call = Assert.Single(store.ScheduleCalls);
        Assert.Equal(post.Id, call.ContentPostId);
        Assert.Equal(workspaceId, call.WorkspaceId);
        Assert.Equal(scheduledAt.UtcDateTime, call.ScheduledAt);
        Assert.Equal(TimeSpan.FromMinutes(15), call.ClaimExpiry);
    }

    [Fact]
    public async Task A_configured_claim_expiry_is_used_for_the_schedule_guard()
    {
        var workspaceId = Guid.NewGuid();
        await using var dbContext = CreateDbContext();
        var post = SeedPost(dbContext, workspaceId, ContentPostStatus.Approved);
        var store = new StubScheduledPostStore();
        var service = CreateService(
            dbContext,
            workspaceId,
            store,
            new SchedulingOptions { ClaimExpiryMinutes = 45 });

        await service.ScheduleAsync(
            post.Id,
            new ScheduleContentPostRequest { ScheduledAt = DateTimeOffset.UtcNow.AddHours(1) });

        Assert.Equal(
            TimeSpan.FromMinutes(45),
            Assert.Single(store.ScheduleCalls).ClaimExpiry);
    }

    [Fact]
    public async Task Draft_cannot_be_scheduled()
    {
        var workspaceId = Guid.NewGuid();
        await using var dbContext = CreateDbContext();
        var post = SeedPost(dbContext, workspaceId, ContentPostStatus.Draft);
        var store = new StubScheduledPostStore();
        var service = CreateService(dbContext, workspaceId, store);

        var exception = await Assert.ThrowsAsync<InvalidContentPostTransitionException>(() =>
            service.ScheduleAsync(
                post.Id,
                new ScheduleContentPostRequest { ScheduledAt = DateTimeOffset.UtcNow.AddHours(1) }));

        Assert.Contains("This post is Draft", exception.Message);
        Assert.Equal(ContentPostStatus.Draft, dbContext.ContentPosts.Single().Status);
        Assert.Empty(store.ScheduleCalls);
    }

    [Fact]
    public async Task Rejected_cannot_be_scheduled_and_must_be_approved_again()
    {
        var workspaceId = Guid.NewGuid();
        await using var dbContext = CreateDbContext();
        var post = SeedPost(dbContext, workspaceId, ContentPostStatus.Rejected);
        var store = new StubScheduledPostStore();
        var service = CreateService(dbContext, workspaceId, store);

        var exception = await Assert.ThrowsAsync<InvalidContentPostTransitionException>(() =>
            service.ScheduleAsync(
                post.Id,
                new ScheduleContentPostRequest { ScheduledAt = DateTimeOffset.UtcNow.AddHours(1) }));

        Assert.Contains("This post is Rejected", exception.Message);
        Assert.Equal(ContentPostStatus.Rejected, dbContext.ContentPosts.Single().Status);
        Assert.Null(dbContext.ContentPosts.Single().ScheduledAt);
        Assert.Empty(store.ScheduleCalls);
    }

    [Fact]
    public async Task Published_cannot_be_scheduled()
    {
        var workspaceId = Guid.NewGuid();
        await using var dbContext = CreateDbContext();
        var post = SeedPost(dbContext, workspaceId, ContentPostStatus.Published);
        var store = new StubScheduledPostStore();
        var service = CreateService(dbContext, workspaceId, store);

        var exception = await Assert.ThrowsAsync<InvalidContentPostTransitionException>(() =>
            service.ScheduleAsync(
                post.Id,
                new ScheduleContentPostRequest { ScheduledAt = DateTimeOffset.UtcNow.AddHours(1) }));

        Assert.Contains("This post is Published", exception.Message);
        Assert.Equal(ContentPostStatus.Published, dbContext.ContentPosts.Single().Status);
        Assert.Empty(store.ScheduleCalls);
    }

    [Fact]
    public async Task Rescheduling_a_scheduled_post_uses_the_new_time()
    {
        var workspaceId = Guid.NewGuid();
        await using var dbContext = CreateDbContext();
        var post = SeedPost(dbContext, workspaceId, ContentPostStatus.Scheduled);
        post.ScheduledAt = DateTime.UtcNow.AddHours(1);
        dbContext.SaveChanges();
        var store = new StubScheduledPostStore();
        var service = CreateService(dbContext, workspaceId, store);
        var newTime = DateTimeOffset.UtcNow.AddDays(2);

        var result = await service.ScheduleAsync(
            post.Id,
            new ScheduleContentPostRequest { ScheduledAt = newTime });

        Assert.NotNull(result);
        var call = Assert.Single(store.ScheduleCalls);
        Assert.Equal(newTime.UtcDateTime, call.ScheduledAt);
    }

    [Fact]
    public async Task Rescheduling_while_a_publish_claim_is_held_is_refused()
    {
        // The store refuses the conditional write when a fresh claim is in place.
        // The service must turn that into the transition error, not clear the claim.
        var workspaceId = Guid.NewGuid();
        await using var dbContext = CreateDbContext();
        var post = SeedPost(dbContext, workspaceId, ContentPostStatus.Scheduled);
        var store = new StubScheduledPostStore { ScheduleSucceeds = false };
        var service = CreateService(dbContext, workspaceId, store);
        post.PublishClaimedAt = DateTime.UtcNow.AddMinutes(-2);
        dbContext.SaveChanges();

        var exception = await Assert.ThrowsAsync<InvalidContentPostTransitionException>(() =>
            service.ScheduleAsync(
                post.Id,
                new ScheduleContentPostRequest { ScheduledAt = DateTimeOffset.UtcNow.AddHours(1) }));

        Assert.Contains("currently being published", exception.Message);
        Assert.NotNull(dbContext.ContentPosts.Single().PublishClaimedAt);
    }

    [Fact]
    public async Task A_reschedule_refused_because_of_a_status_change_names_the_new_status()
    {
        var workspaceId = Guid.NewGuid();
        await using var dbContext = CreateDbContext();
        var post = SeedPost(dbContext, workspaceId, ContentPostStatus.Scheduled);
        var store = new StubScheduledPostStore { ScheduleSucceeds = false };
        var service = CreateService(dbContext, workspaceId, store);

        // The store cannot fail for a status the service already saw, so to model
        // the race the database state changes between the service's read of the
        // status and the store's conditional write.
        post.Status = ContentPostStatus.Published;
        await dbContext.SaveChangesAsync();

        var exception = await Assert.ThrowsAsync<InvalidContentPostTransitionException>(() =>
            service.ScheduleAsync(
                post.Id,
                new ScheduleContentPostRequest { ScheduledAt = DateTimeOffset.UtcNow.AddHours(1) }));

        Assert.Contains("This post is Published", exception.Message);
    }

    [Theory]
    [InlineData(-60)]
    [InlineData(-1)]
    [InlineData(0)]
    public async Task ScheduledAt_must_be_in_the_future(int offsetSeconds)
    {
        var workspaceId = Guid.NewGuid();
        await using var dbContext = CreateDbContext();
        var post = SeedPost(dbContext, workspaceId, ContentPostStatus.Approved);
        var store = new StubScheduledPostStore();
        var service = CreateService(dbContext, workspaceId, store);

        var exception = await Assert.ThrowsAsync<InvalidContentPostScheduleTimeException>(() =>
            service.ScheduleAsync(
                post.Id,
                new ScheduleContentPostRequest
                {
                    ScheduledAt = DateTimeOffset.UtcNow.AddSeconds(offsetSeconds)
                }));

        Assert.Contains("must be in the future", exception.Message);
        Assert.Equal(ContentPostStatus.Approved, dbContext.ContentPosts.Single().Status);
        Assert.Null(dbContext.ContentPosts.Single().ScheduledAt);
        Assert.Empty(store.ScheduleCalls);
    }

    [Fact]
    public async Task Scheduling_does_not_write_through_the_tracked_entity()
    {
        // The service leaves persistence to the store's single conditional write,
        // so calling it must not mutate the tracked post on its own. The store is
        // responsible for the state change; this guards against the service quietly
        // scheduling twice through the change tracker.
        var workspaceId = Guid.NewGuid();
        await using var dbContext = CreateDbContext();
        var post = SeedPost(dbContext, workspaceId, ContentPostStatus.Approved);
        var store = new StubScheduledPostStore();
        var service = CreateService(dbContext, workspaceId, store);

        await service.ScheduleAsync(
            post.Id,
            new ScheduleContentPostRequest { ScheduledAt = DateTimeOffset.UtcNow.AddHours(5) });

        var stored = dbContext.ContentPosts.Single();
        Assert.Equal(ContentPostStatus.Approved, stored.Status);
        Assert.Null(stored.ScheduledAt);
        Assert.Single(store.ScheduleCalls);
    }

    [Fact]
    public async Task Scheduling_a_post_from_another_workspace_returns_null()
    {
        var owningWorkspaceId = Guid.NewGuid();
        var otherWorkspaceId = Guid.NewGuid();
        await using var dbContext = CreateDbContext();
        var post = SeedPost(dbContext, owningWorkspaceId, ContentPostStatus.Approved);
        var store = new StubScheduledPostStore();
        var service = CreateService(dbContext, otherWorkspaceId, store);

        var result = await service.ScheduleAsync(
            post.Id,
            new ScheduleContentPostRequest { ScheduledAt = DateTimeOffset.UtcNow.AddHours(1) });

        Assert.Null(result);
        Assert.Equal(ContentPostStatus.Approved, dbContext.ContentPosts.Single().Status);
        Assert.Empty(store.ScheduleCalls);
    }

    [Fact]
    public async Task Scheduling_never_marks_a_post_published()
    {
        var workspaceId = Guid.NewGuid();
        await using var dbContext = CreateDbContext();
        var post = SeedPost(dbContext, workspaceId, ContentPostStatus.Approved);
        var store = new StubScheduledPostStore();
        var service = CreateService(dbContext, workspaceId, store);

        var result = await service.ScheduleAsync(
            post.Id,
            new ScheduleContentPostRequest { ScheduledAt = DateTimeOffset.UtcNow.AddHours(1) });

        Assert.NotEqual("Published", result!.Status);
        Assert.Equal(ContentPostStatus.Approved, dbContext.ContentPosts.Single().Status);
    }

    [Fact]
    public async Task Schedule_request_null_is_rejected()
    {
        var workspaceId = Guid.NewGuid();
        await using var dbContext = CreateDbContext();
        var post = SeedPost(dbContext, workspaceId, ContentPostStatus.Approved);
        var service = CreateService(dbContext, workspaceId, new StubScheduledPostStore());

        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            service.ScheduleAsync(post.Id, null!));
    }

    private static AppDbContext CreateDbContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"scheduling-{Guid.NewGuid()}")
            .ConfigureWarnings(warnings => warnings
                .Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options);

    private static ContentPostService CreateService(
        AppDbContext dbContext,
        Guid workspaceId,
        StubScheduledPostStore store,
        SchedulingOptions? schedulingOptions = null) =>
        new(
            dbContext,
            CreateCurrentUser(workspaceId),
            AuditLogTestContext.Create(dbContext, workspaceId),
            new StubAiProvider("{}"),
            AiRunTestContext.Create(dbContext, workspaceId),
            new ContentPostPromptBuilder(),
            new ContentEvaluationPromptBuilder(),
            new ContentRepetitionDetector(),
            store,
            NullLogger<ContentPostService>.Instance,
            Options.Create(new AiOptions
            {
                Provider = "Gemini",
                Model = "test-model",
                ApiKey = "test-placeholder"
            }),
            Options.Create(new ContentQualityOptions()),
            Options.Create(schedulingOptions ?? new SchedulingOptions()));

    private static CurrentUserService CreateCurrentUser(Guid workspaceId)
    {
        var accessor = new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                [
                    new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
                    new Claim("workspace_id", workspaceId.ToString())
                ], "test"))
            }
        };
        return new CurrentUserService(accessor);
    }

    private static ContentPost SeedPost(
        AppDbContext dbContext,
        Guid workspaceId,
        ContentPostStatus status)
    {
        var post = new ContentPost
        {
            Id = Guid.NewGuid(),
            WorkspaceId = workspaceId,
            ContentIdeaId = Guid.NewGuid(),
            Platform = ContentPlatform.LinkedIn,
            Status = status,
            Content = "Existing content",
            Hook = "Existing hook",
            CallToAction = string.Empty,
            Hashtags = ["#AI"],
            ContentType = ContentType.Insight,
            StorytellingStructure = "Hook -> Insight",
            CreatedAt = OldTimestamp,
            UpdatedAt = OldTimestamp
        };
        dbContext.ContentPosts.Add(post);
        dbContext.SaveChanges();
        return post;
    }

    private readonly record struct ScheduleCall(
        Guid ContentPostId,
        Guid WorkspaceId,
        DateTime ScheduledAt,
        DateTime Now,
        TimeSpan ClaimExpiry);

    private sealed class StubScheduledPostStore : IScheduledPostStore
    {
        public List<ScheduleCall> ScheduleCalls { get; } = [];

        public bool ScheduleSucceeds { get; init; } = true;

        public Task<bool> TryScheduleAsync(
            Guid contentPostId,
            Guid workspaceId,
            DateTime scheduledAt,
            DateTime now,
            TimeSpan claimExpiry,
            CancellationToken cancellationToken)
        {
            ScheduleCalls.Add(new ScheduleCall(contentPostId, workspaceId, scheduledAt, now, claimExpiry));
            return Task.FromResult(ScheduleSucceeds);
        }

        public Task<IReadOnlyList<ScheduledPostCandidate>> FindDueAsync(
            DateTime now,
            int limit,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<bool> TryClaimAsync(
            Guid contentPostId,
            DateTime now,
            TimeSpan claimExpiry,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<bool> TryClaimForImmediatePublishAsync(
            Guid contentPostId,
            Guid workspaceId,
            DateTime now,
            TimeSpan claimExpiry,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<bool> TryCancelScheduleAsync(
            Guid contentPostId,
            Guid workspaceId,
            DateTime now,
            TimeSpan claimExpiry,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<ScheduledPostPayload?> GetPublishPayloadAsync(
            Guid contentPostId,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<PublishAccountCandidate>> FindPublishAccountsAsync(
            Guid workspaceId,
            ContentPlatform platform,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<bool> TryMarkPublishedAsync(
            Guid contentPostId,
            DateTime claimedAt,
            DateTime publishedAt,
            string? externalPostId,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<bool> TryRecordPublishFailureAsync(
            Guid contentPostId,
            DateTime claimedAt,
            PublishFailureReason reason,
            DateTime failedAt,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    private sealed class StubAiProvider(string response) : IAiProvider
    {
        public Task<string> GenerateAsync(
            string systemPrompt,
            string userPrompt,
            AiResponseSchema responseSchema,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(response);
    }
}