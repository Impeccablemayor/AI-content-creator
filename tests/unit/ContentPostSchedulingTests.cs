using System.Security.Claims;
using Api.Application.AI;
using Api.Application.AI.Prompts;
using Api.Application.Authentication;
using Api.Application.ContentEvaluation;
using Api.Application.ContentIdeas;
using Api.Application.ContentPosts;
using Api.Application.Services;
using Api.Configuration;
using Api.Contracts.ContentPost;
using Api.Domain.Entities;
using Api.Domain.Enums;
using Api.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Api.Tests;

public sealed class ContentPostSchedulingTests
{
    private static readonly DateTime OldTimestamp = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Approved_post_can_be_scheduled()
    {
        var workspaceId = Guid.NewGuid();
        await using var dbContext = CreateDbContext();
        var post = SeedPost(dbContext, workspaceId, ContentPostStatus.Approved);
        var service = CreateService(dbContext, workspaceId);
        var scheduledAt = DateTimeOffset.UtcNow.AddHours(3);

        var result = await service.ScheduleAsync(
            post.Id,
            new ScheduleContentPostRequest { ScheduledAt = scheduledAt });

        Assert.NotNull(result);
        Assert.Equal("Scheduled", result.Status);
        Assert.Equal(scheduledAt.UtcDateTime, result.ScheduledAt);

        var stored = dbContext.ContentPosts.Single();
        Assert.Equal(ContentPostStatus.Scheduled, stored.Status);
        Assert.Equal(scheduledAt.UtcDateTime, stored.ScheduledAt);
    }

    [Fact]
    public async Task Draft_cannot_be_scheduled()
    {
        var workspaceId = Guid.NewGuid();
        await using var dbContext = CreateDbContext();
        var post = SeedPost(dbContext, workspaceId, ContentPostStatus.Draft);
        var service = CreateService(dbContext, workspaceId);

        var exception = await Assert.ThrowsAsync<InvalidContentPostTransitionException>(() =>
            service.ScheduleAsync(
                post.Id,
                new ScheduleContentPostRequest { ScheduledAt = DateTimeOffset.UtcNow.AddHours(1) }));

        Assert.Contains("This post is Draft", exception.Message);
        Assert.Equal(ContentPostStatus.Draft, dbContext.ContentPosts.Single().Status);
    }

    [Fact]
    public async Task Rejected_cannot_be_scheduled_and_must_be_approved_again()
    {
        var workspaceId = Guid.NewGuid();
        await using var dbContext = CreateDbContext();
        var post = SeedPost(dbContext, workspaceId, ContentPostStatus.Rejected);
        var service = CreateService(dbContext, workspaceId);

        var exception = await Assert.ThrowsAsync<InvalidContentPostTransitionException>(() =>
            service.ScheduleAsync(
                post.Id,
                new ScheduleContentPostRequest { ScheduledAt = DateTimeOffset.UtcNow.AddHours(1) }));

        Assert.Contains("This post is Rejected", exception.Message);
        Assert.Equal(ContentPostStatus.Rejected, dbContext.ContentPosts.Single().Status);
        Assert.Null(dbContext.ContentPosts.Single().ScheduledAt);
    }

    [Fact]
    public async Task Published_cannot_be_scheduled()
    {
        var workspaceId = Guid.NewGuid();
        await using var dbContext = CreateDbContext();
        var post = SeedPost(dbContext, workspaceId, ContentPostStatus.Published);
        var service = CreateService(dbContext, workspaceId);

        var exception = await Assert.ThrowsAsync<InvalidContentPostTransitionException>(() =>
            service.ScheduleAsync(
                post.Id,
                new ScheduleContentPostRequest { ScheduledAt = DateTimeOffset.UtcNow.AddHours(1) }));

        Assert.Contains("This post is Published", exception.Message);
        Assert.Equal(ContentPostStatus.Published, dbContext.ContentPosts.Single().Status);
    }

    [Fact]
    public async Task Scheduled_post_can_be_rescheduled()
    {
        var workspaceId = Guid.NewGuid();
        await using var dbContext = CreateDbContext();
        var post = SeedPost(dbContext, workspaceId, ContentPostStatus.Scheduled);
        post.ScheduledAt = DateTime.UtcNow.AddHours(1);
        dbContext.SaveChanges();
        var service = CreateService(dbContext, workspaceId);
        var newTime = DateTimeOffset.UtcNow.AddDays(2);

        var result = await service.ScheduleAsync(
            post.Id,
            new ScheduleContentPostRequest { ScheduledAt = newTime });

        Assert.NotNull(result);
        Assert.Equal("Scheduled", result.Status);
        Assert.Equal(newTime.UtcDateTime, result.ScheduledAt);
    }

    [Fact]
    public async Task Rescheduling_clears_a_previous_publish_claim()
    {
        var workspaceId = Guid.NewGuid();
        await using var dbContext = CreateDbContext();
        var post = SeedPost(dbContext, workspaceId, ContentPostStatus.Scheduled);
        post.ScheduledAt = DateTime.UtcNow.AddHours(1);
        post.PublishClaimedAt = DateTime.UtcNow.AddMinutes(-5);
        dbContext.SaveChanges();
        var service = CreateService(dbContext, workspaceId);

        await service.ScheduleAsync(
            post.Id,
            new ScheduleContentPostRequest { ScheduledAt = DateTimeOffset.UtcNow.AddDays(1) });

        Assert.Null(dbContext.ContentPosts.Single().PublishClaimedAt);
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
        var service = CreateService(dbContext, workspaceId);

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
    }

    [Fact]
    public async Task Scheduling_preserves_the_post_content_and_advances_UpdatedAt()
    {
        var workspaceId = Guid.NewGuid();
        await using var dbContext = CreateDbContext();
        var post = SeedPost(dbContext, workspaceId, ContentPostStatus.Approved);
        var service = CreateService(dbContext, workspaceId);
        var before = post.Content;

        await service.ScheduleAsync(
            post.Id,
            new ScheduleContentPostRequest { ScheduledAt = DateTimeOffset.UtcNow.AddHours(5) });

        var stored = dbContext.ContentPosts.Single();
        Assert.Equal(before, stored.Content);
        Assert.Equal("Existing hook", stored.Hook);
        Assert.Equal(ContentPlatform.LinkedIn, stored.Platform);
        Assert.Equal(post.CreatedAt, stored.CreatedAt);
        Assert.True(
            stored.UpdatedAt > OldTimestamp,
            "UpdatedAt must advance when a post is scheduled.");
    }

    [Fact]
    public async Task Scheduling_a_post_from_another_workspace_returns_null()
    {
        var owningWorkspaceId = Guid.NewGuid();
        var otherWorkspaceId = Guid.NewGuid();
        await using var dbContext = CreateDbContext();
        var post = SeedPost(dbContext, owningWorkspaceId, ContentPostStatus.Approved);
        var service = CreateService(dbContext, otherWorkspaceId);

        var result = await service.ScheduleAsync(
            post.Id,
            new ScheduleContentPostRequest { ScheduledAt = DateTimeOffset.UtcNow.AddHours(1) });

        Assert.Null(result);
        var stored = dbContext.ContentPosts.Single();
        Assert.Equal(ContentPostStatus.Approved, stored.Status);
        Assert.Null(stored.ScheduledAt);
    }

    [Fact]
    public async Task Scheduling_never_marks_a_post_published()
    {
        var workspaceId = Guid.NewGuid();
        await using var dbContext = CreateDbContext();
        var post = SeedPost(dbContext, workspaceId, ContentPostStatus.Approved);
        var service = CreateService(dbContext, workspaceId);

        var result = await service.ScheduleAsync(
            post.Id,
            new ScheduleContentPostRequest { ScheduledAt = DateTimeOffset.UtcNow.AddHours(1) });

        Assert.NotEqual("Published", result!.Status);
        Assert.NotEqual(ContentPostStatus.Published, dbContext.ContentPosts.Single().Status);
    }

    [Fact]
    public async Task Schedule_request_null_is_rejected()
    {
        var workspaceId = Guid.NewGuid();
        await using var dbContext = CreateDbContext();
        var post = SeedPost(dbContext, workspaceId, ContentPostStatus.Approved);
        var service = CreateService(dbContext, workspaceId);

        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            service.ScheduleAsync(post.Id, null!));
    }

    private static AppDbContext CreateDbContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"scheduling-{Guid.NewGuid()}")
            .Options);

    private static ContentPostService CreateService(
        AppDbContext dbContext,
        Guid workspaceId) =>
        new(
            dbContext,
            CreateCurrentUser(workspaceId),
            AuditLogTestContext.Create(dbContext, workspaceId),
            new StubAiProvider("{}"),
            AiRunTestContext.Create(dbContext, workspaceId),
            new ContentPostPromptBuilder(),
            new ContentEvaluationPromptBuilder(),
            new ContentRepetitionDetector(),
            NullLogger<ContentPostService>.Instance,
            Options.Create(new AiOptions
            {
                Provider = "Gemini",
                Model = "test-model",
                ApiKey = "test-placeholder"
            }),
            Options.Create(new ContentQualityOptions()));

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
