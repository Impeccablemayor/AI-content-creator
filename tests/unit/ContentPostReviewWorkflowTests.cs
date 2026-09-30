using System.Security.Claims;
using Api.Application.AI;
using Api.Application.AI.Prompts;
using Api.Application.Authentication;
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

public sealed class ContentPostReviewWorkflowTests
{
    private static readonly DateTime OldTimestamp = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task List_returns_only_the_current_workspace_newest_first()
    {
        var workspaceId = Guid.NewGuid();
        var otherWorkspaceId = Guid.NewGuid();
        await using var dbContext = CreateDbContext();
        var idea = AddIdea(dbContext, workspaceId);
        SeedOlder(dbContext, idea.Id, workspaceId, ContentPostStatus.Approved, "Approved", OldTimestamp);
        SeedOlder(dbContext, idea.Id, workspaceId, ContentPostStatus.Draft, "Newest", OldTimestamp.AddHours(2));
        SeedOlder(dbContext, idea.Id, workspaceId, ContentPostStatus.Draft, "Oldest", OldTimestamp.AddHours(1));
        SeedOlder(dbContext, idea.Id, otherWorkspaceId, ContentPostStatus.Draft, "Foreign", OldTimestamp.AddHours(3));
        var service = CreateService(dbContext, workspaceId, new StubAiProvider(CreateValidResponse("LinkedIn")));

        var result = await service.GetAsync(null, null);

        Assert.Equal(["Newest", "Oldest", "Approved"], result.Select(post => post.Content));
    }

    [Fact]
    public async Task List_filters_by_status_and_platform()
    {
        var workspaceId = Guid.NewGuid();
        await using var dbContext = CreateDbContext();
        var idea = AddIdea(dbContext, workspaceId);
        SeedOlder(dbContext, idea.Id, workspaceId, ContentPostStatus.Draft, "Draft post", OldTimestamp);
        SeedOlder(dbContext, idea.Id, workspaceId, ContentPostStatus.Approved, "Approved post", OldTimestamp);
        SeedOlder(dbContext, idea.Id, workspaceId, ContentPostStatus.Draft, "X post", OldTimestamp.AddHours(1), ContentPlatform.X);        var service = CreateService(dbContext, workspaceId, new StubAiProvider(CreateValidResponse("LinkedIn")));

        var drafts = await service.GetAsync(ContentPostStatus.Draft, null);
        var xDrafts = await service.GetAsync(ContentPostStatus.Draft, ContentPlatform.X);
        var approvedX = await service.GetAsync(ContentPostStatus.Approved, ContentPlatform.X);

        Assert.Equal(["X post", "Draft post"], drafts.Select(post => post.Content));
        Assert.Equal(["X post"], xDrafts.Select(post => post.Content));
        Assert.Empty(approvedX);
    }

    [Fact]
    public async Task Get_returns_the_workspace_post_and_hides_foreign_workspaces()
    {
        var workspaceId = Guid.NewGuid();
        var otherWorkspaceId = Guid.NewGuid();
        await using var dbContext = CreateDbContext();
        var idea = AddIdea(dbContext, workspaceId);
        var own = SeedOlder(dbContext, idea.Id, workspaceId, ContentPostStatus.Draft, "Mine", OldTimestamp);
        var foreign = SeedOlder(dbContext, idea.Id, otherWorkspaceId, ContentPostStatus.Draft, "Theirs", OldTimestamp);
        var service = CreateService(dbContext, workspaceId, new StubAiProvider(CreateValidResponse("LinkedIn")));

        var found = await service.GetAsync(own.Id);
        var hidden = await service.GetAsync(foreign.Id);
        var missing = await service.GetAsync(Guid.NewGuid());

        Assert.NotNull(found);
        Assert.Equal("Mine", found.Content);
        Assert.Equal(OldTimestamp, found.CreatedAt);
        Assert.Equal(OldTimestamp, found.UpdatedAt);
        Assert.Null(hidden);
        Assert.Null(missing);
    }

    [Fact]
    public async Task Update_replaces_editable_fields_only_and_bumps_updated_at()
    {
        var workspaceId = Guid.NewGuid();
        await using var dbContext = CreateDbContext();
        var idea = AddIdea(dbContext, workspaceId);
        var post = SeedOlder(dbContext, idea.Id, workspaceId, ContentPostStatus.Draft, "Before", OldTimestamp);
        var service = CreateService(dbContext, workspaceId, new StubAiProvider(CreateValidResponse("LinkedIn")));

        var result = await service.UpdateAsync(post.Id, new UpdateContentPostRequest
        {
            Content = "A hand written post",
            Hook = "A hand written hook",
            CallToAction = "Try it today",
            Hashtags = ["AI", "#viral", "#ai"],
            ContentType = ContentType.Insight,
            StorytellingStructure = "Hook → Insight"
        });

        Assert.NotNull(result);
        Assert.Equal(post.Id, result.Id);
        Assert.Equal(idea.Id, result.ContentIdeaId);
        Assert.Equal("LinkedIn", result.Platform);
        Assert.Equal("Draft", result.Status);
        Assert.Equal(ContentType.Insight.ToString(), result.ContentType);
        Assert.Equal(["#AI"], result.Hashtags);
        Assert.Equal("A hand written post", result.Content);
        Assert.Equal("A hand written hook", result.Hook);
        Assert.Equal("Try it today", result.CallToAction);
        Assert.Equal("Hook → Insight", result.StorytellingStructure);
        Assert.Equal(OldTimestamp, result.CreatedAt);
        Assert.True(result.UpdatedAt > OldTimestamp, $"Expected a bumped UpdatedAt, got {result.UpdatedAt:O}.");
        Assert.Equal(1, dbContext.ContentPosts.Count());
    }

    [Fact]
    public async Task Update_trims_content_and_rejects_oversized_x_content()
    {
        var workspaceId = Guid.NewGuid();
        await using var dbContext = CreateDbContext();
        var idea = AddIdea(dbContext, workspaceId);
        var post = SeedOlder(dbContext, idea.Id, workspaceId, ContentPostStatus.Draft, "Before", OldTimestamp, ContentPlatform.X);
        var service = CreateService(dbContext, workspaceId, new StubAiProvider(CreateValidResponse("X")));

        var result = await service.UpdateAsync(post.Id, new UpdateContentPostRequest
        {
            Content = "  A short X post  ",
            Hook = "  Hook  ",
            CallToAction = "",
            Hashtags = [],
            ContentType = ContentType.Insight,
            StorytellingStructure = "  Hook → CTA  "
        });

        Assert.NotNull(result);
        Assert.Equal("A short X post", result.Content);
        Assert.Equal("Hook", result.Hook);
        Assert.Equal(string.Empty, result.CallToAction);
        Assert.Empty(result.Hashtags);

        await Assert.ThrowsAsync<InvalidContentPostContentException>(() =>
            service.UpdateAsync(post.Id, new UpdateContentPostRequest
            {
                Content = new string('x', 281),
                Hook = "Hook",
                CallToAction = "",
                Hashtags = [],
                ContentType = ContentType.Insight,
                StorytellingStructure = "Hook → CTA"
            }));

        Assert.Equal("A short X post", dbContext.ContentPosts.Single().Content);
    }

    [Fact]
    public async Task Update_rejects_an_undefined_content_type()
    {
        var workspaceId = Guid.NewGuid();
        await using var dbContext = CreateDbContext();
        var idea = AddIdea(dbContext, workspaceId);
        var post = SeedOlder(dbContext, idea.Id, workspaceId, ContentPostStatus.Draft, "Before", OldTimestamp);
        var service = CreateService(dbContext, workspaceId, new StubAiProvider(CreateValidResponse("LinkedIn")));

        var request = CreateRequest("Edited");
        request.ContentType = (ContentType)0;

        await Assert.ThrowsAsync<InvalidContentPostContentException>(() =>
            service.UpdateAsync(post.Id, request));

        Assert.Equal("Before", dbContext.ContentPosts.Single().Content);
    }

    [Theory]
    [InlineData(ContentPostStatus.Approved)]
    [InlineData(ContentPostStatus.Scheduled)]
    [InlineData(ContentPostStatus.Published)]
    [InlineData(ContentPostStatus.Rejected)]
    public async Task Update_is_blocked_outside_draft(ContentPostStatus status)
    {
        var workspaceId = Guid.NewGuid();
        await using var dbContext = CreateDbContext();
        var idea = AddIdea(dbContext, workspaceId);
        var post = SeedOlder(dbContext, idea.Id, workspaceId, status, "Locked", OldTimestamp);
        var service = CreateService(dbContext, workspaceId, new StubAiProvider(CreateValidResponse("LinkedIn")));

        var exception = await Assert.ThrowsAsync<InvalidContentPostTransitionException>(() =>
            service.UpdateAsync(post.Id, CreateRequest("Overwritten")));

        Assert.Contains(status.ToString(), exception.Message);
        Assert.Equal("Locked", dbContext.ContentPosts.Single().Content);
    }

    [Fact]
    public async Task Update_ignores_posts_from_other_workspaces()
    {
        var workspaceId = Guid.NewGuid();
        var otherWorkspaceId = Guid.NewGuid();
        await using var dbContext = CreateDbContext();
        var idea = AddIdea(dbContext, otherWorkspaceId);
        var foreign = SeedOlder(dbContext, idea.Id, otherWorkspaceId, ContentPostStatus.Draft, "Theirs", OldTimestamp);
        var service = CreateService(dbContext, workspaceId, new StubAiProvider(CreateValidResponse("LinkedIn")));

        Assert.Null(await service.UpdateAsync(foreign.Id, CreateRequest("Overwritten")));
        Assert.Equal("Theirs", dbContext.ContentPosts.Single().Content);
    }

    [Fact]
    public async Task Regenerate_reuses_the_post_identity_and_bumps_updated_at()
    {
        var workspaceId = Guid.NewGuid();
        await using var dbContext = CreateDbContext();
        var idea = AddIdea(dbContext, workspaceId);
        SeedContext(dbContext, workspaceId);
        var post = SeedOlder(dbContext, idea.Id, workspaceId, ContentPostStatus.Draft, "Before", OldTimestamp);
        var aiProvider = new StubAiProvider(CreateValidResponse("LinkedIn"));
        var service = CreateService(dbContext, workspaceId, aiProvider);

        var result = await service.RegenerateAsync(post.Id);

        Assert.NotNull(result);
        Assert.Equal(post.Id, result.Id);
        Assert.Equal(idea.Id, result.ContentIdeaId);
        Assert.Equal("Draft", result.Status);
        Assert.Equal("Educational", result.ContentType);
        Assert.Equal(["#AI", "#SoftwareEngineering"], result.Hashtags);
        Assert.StartsWith("A useful opening", result.Content);
        Assert.Equal(OldTimestamp, result.CreatedAt);
        Assert.True(result.UpdatedAt > OldTimestamp);
        Assert.Equal(1, aiProvider.RequestCount);
        Assert.Same(ContentPostResponseSchema.Value, aiProvider.ResponseSchema);
        Assert.Equal(1, dbContext.ContentPosts.Count());
    }

    [Fact]
    public async Task Regenerate_keeps_the_post_platform_and_does_not_create_a_second_post()
    {
        var workspaceId = Guid.NewGuid();
        await using var dbContext = CreateDbContext();
        var idea = AddIdea(dbContext, workspaceId);
        SeedContext(dbContext, workspaceId);
        var post = SeedOlder(dbContext, idea.Id, workspaceId, ContentPostStatus.Draft, "Before", OldTimestamp, ContentPlatform.X);
        var aiProvider = new StubAiProvider(CreateValidResponse("X"));
        var service = CreateService(dbContext, workspaceId, aiProvider);

        var result = await service.RegenerateAsync(post.Id);

        Assert.NotNull(result);
        Assert.Equal("X", result.Platform);
        Assert.Contains("X", aiProvider.UserPrompt);
        Assert.Equal(1, dbContext.ContentPosts.Count());
    }

    [Fact]
    public async Task Regenerating_a_rejected_post_returns_it_to_draft()
    {
        var workspaceId = Guid.NewGuid();
        await using var dbContext = CreateDbContext();
        var idea = AddIdea(dbContext, workspaceId);
        SeedContext(dbContext, workspaceId);
        var post = SeedOlder(dbContext, idea.Id, workspaceId, ContentPostStatus.Rejected, "Rejected", OldTimestamp);
        var service = CreateService(dbContext, workspaceId, new StubAiProvider(CreateValidResponse("LinkedIn")));

        var result = await service.RegenerateAsync(post.Id);

        Assert.NotNull(result);
        Assert.Equal("Draft", result.Status);
    }

    [Theory]
    [InlineData(ContentPostStatus.Approved)]
    [InlineData(ContentPostStatus.Scheduled)]
    [InlineData(ContentPostStatus.Published)]
    public async Task Regenerate_is_blocked_for_settled_posts(ContentPostStatus status)
    {
        var workspaceId = Guid.NewGuid();
        await using var dbContext = CreateDbContext();
        var idea = AddIdea(dbContext, workspaceId);
        SeedContext(dbContext, workspaceId);
        var post = SeedOlder(dbContext, idea.Id, workspaceId, status, "Settled", OldTimestamp);
        var aiProvider = new StubAiProvider(CreateValidResponse("LinkedIn"));
        var service = CreateService(dbContext, workspaceId, aiProvider);

        var exception = await Assert.ThrowsAsync<InvalidContentPostTransitionException>(() =>
            service.RegenerateAsync(post.Id));

        Assert.Contains(status.ToString(), exception.Message);
        Assert.Equal(0, aiProvider.RequestCount);
        Assert.Equal("Settled", dbContext.ContentPosts.Single().Content);
    }

    [Fact]
    public async Task Regenerate_ignores_posts_from_other_workspaces_and_does_not_call_ai()
    {
        var workspaceId = Guid.NewGuid();
        var otherWorkspaceId = Guid.NewGuid();
        await using var dbContext = CreateDbContext();
        var idea = AddIdea(dbContext, otherWorkspaceId);
        SeedContext(dbContext, otherWorkspaceId);
        var foreign = SeedOlder(dbContext, idea.Id, otherWorkspaceId, ContentPostStatus.Draft, "Theirs", OldTimestamp);
        var aiProvider = new StubAiProvider(CreateValidResponse("LinkedIn"));
        var service = CreateService(dbContext, workspaceId, aiProvider);

        Assert.Null(await service.RegenerateAsync(foreign.Id));
        Assert.Equal(0, aiProvider.RequestCount);
        Assert.Equal("Theirs", dbContext.ContentPosts.Single().Content);
    }

    [Fact]
    public async Task Regenerate_requires_workspace_brand_audience_and_strategy()
    {
        var workspaceId = Guid.NewGuid();
        await using var dbContext = CreateDbContext();
        var idea = AddIdea(dbContext, workspaceId);
        var post = SeedOlder(dbContext, idea.Id, workspaceId, ContentPostStatus.Draft, "Before", OldTimestamp);
        var aiProvider = new StubAiProvider(CreateValidResponse("LinkedIn"));
        var service = CreateService(dbContext, workspaceId, aiProvider);

        await Assert.ThrowsAsync<ContentIdeaContextIncompleteException>(() =>
            service.RegenerateAsync(post.Id));

        Assert.Equal(0, aiProvider.RequestCount);
    }

    [Fact]
    public async Task Approve_moves_a_draft_to_approved()
    {
        var workspaceId = Guid.NewGuid();
        await using var dbContext = CreateDbContext();
        var idea = AddIdea(dbContext, workspaceId);
        var post = SeedOlder(dbContext, idea.Id, workspaceId, ContentPostStatus.Draft, "Ready", OldTimestamp);
        var service = CreateService(dbContext, workspaceId, new StubAiProvider(CreateValidResponse("LinkedIn")));

        var result = await service.ApproveAsync(post.Id);

        Assert.NotNull(result);
        Assert.Equal("Approved", result.Status);
        Assert.Equal("Ready", result.Content);
        Assert.True(result.UpdatedAt > OldTimestamp);
    }

    [Fact]
    public async Task Reject_moves_a_draft_to_rejected_and_never_publishes()
    {
        var workspaceId = Guid.NewGuid();
        await using var dbContext = CreateDbContext();
        var idea = AddIdea(dbContext, workspaceId);
        var post = SeedOlder(dbContext, idea.Id, workspaceId, ContentPostStatus.Draft, "Not ready", OldTimestamp);
        var service = CreateService(dbContext, workspaceId, new StubAiProvider(CreateValidResponse("LinkedIn")));

        var result = await service.RejectAsync(post.Id);

        Assert.NotNull(result);
        Assert.Equal("Rejected", result.Status);
        Assert.Equal(ContentPostStatus.Rejected, dbContext.ContentPosts.Single().Status);
    }

    [Theory]
    [InlineData(ContentPostStatus.Approved)]
    [InlineData(ContentPostStatus.Scheduled)]
    [InlineData(ContentPostStatus.Published)]
    [InlineData(ContentPostStatus.Rejected)]
    public async Task Approve_is_blocked_outside_draft(ContentPostStatus status) =>
        await AssertTransitionBlockedAsync(status, (service, id) => service.ApproveAsync(id), "approved");

    [Theory]
    [InlineData(ContentPostStatus.Approved)]
    [InlineData(ContentPostStatus.Scheduled)]
    [InlineData(ContentPostStatus.Published)]
    [InlineData(ContentPostStatus.Rejected)]
    public async Task Reject_is_blocked_outside_draft(ContentPostStatus status) =>
        await AssertTransitionBlockedAsync(status, (service, id) => service.RejectAsync(id), "rejected");

    [Fact]
    public async Task Approve_and_reject_ignore_posts_from_other_workspaces()
    {
        var workspaceId = Guid.NewGuid();
        var otherWorkspaceId = Guid.NewGuid();
        await using var dbContext = CreateDbContext();
        var idea = AddIdea(dbContext, otherWorkspaceId);
        var foreign = SeedOlder(dbContext, idea.Id, otherWorkspaceId, ContentPostStatus.Draft, "Theirs", OldTimestamp);
        var service = CreateService(dbContext, workspaceId, new StubAiProvider(CreateValidResponse("LinkedIn")));

        Assert.Null(await service.ApproveAsync(foreign.Id));
        Assert.Null(await service.RejectAsync(foreign.Id));
        Assert.Equal(ContentPostStatus.Draft, dbContext.ContentPosts.Single().Status);
    }

    [Fact]
    public async Task Approve_and_reject_return_null_for_unknown_posts()
    {
        var workspaceId = Guid.NewGuid();
        await using var dbContext = CreateDbContext();
        var service = CreateService(dbContext, workspaceId, new StubAiProvider(CreateValidResponse("LinkedIn")));

        Assert.Null(await service.ApproveAsync(Guid.NewGuid()));
        Assert.Null(await service.RejectAsync(Guid.NewGuid()));
        Assert.Null(await service.RegenerateAsync(Guid.NewGuid()));
        Assert.Empty(dbContext.ContentPosts);
    }

    private static async Task AssertTransitionBlockedAsync(
        ContentPostStatus status,
        Func<ContentPostService, Guid, Task<ContentPostResponse?>> transition,
        string expectedMessage)
    {
        var workspaceId = Guid.NewGuid();
        await using var dbContext = CreateDbContext();
        var idea = AddIdea(dbContext, workspaceId);
        var post = SeedOlder(dbContext, idea.Id, workspaceId, status, "Untouched", OldTimestamp);
        var service = CreateService(dbContext, workspaceId, new StubAiProvider(CreateValidResponse("LinkedIn")));

        var exception = await Assert.ThrowsAsync<InvalidContentPostTransitionException>(() =>
            transition(service, post.Id));

        Assert.Contains(expectedMessage, exception.Message);
        Assert.Contains(status.ToString(), exception.Message);
        Assert.Equal(status, dbContext.ContentPosts.Single().Status);
        Assert.Equal("Untouched", dbContext.ContentPosts.Single().Content);
    }

    private static UpdateContentPostRequest CreateRequest(string content) => new()
    {
        Content = content,
        Hook = "Hook",
        CallToAction = string.Empty,
        Hashtags = [],
        ContentType = ContentType.Insight,
        StorytellingStructure = "Hook → Insight"
    };

    private static AppDbContext CreateDbContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static ContentPostService CreateService(
        AppDbContext dbContext,
        Guid workspaceId,
        StubAiProvider aiProvider) =>
        new(
            dbContext,
            CreateCurrentUser(workspaceId),
            aiProvider,
            new ContentPostPromptBuilder(),
            NullLogger<ContentPostService>.Instance,
            Options.Create(new AiOptions
            {
                Provider = "Gemini",
                Model = "test-model",
                ApiKey = "test-placeholder"
            }));

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

    private static ContentIdea AddIdea(AppDbContext dbContext, Guid workspaceId)
    {
        var idea = new ContentIdea
        {
            Id = Guid.NewGuid(),
            WorkspaceId = workspaceId,
            Status = ContentIdeaStatus.Approved,
            Platform = ContentPlatform.LinkedIn,
            Title = "Practical AI debugging",
            Topic = "AI for software developers",
            Angle = "A practical debugging framework",
            Hook = "What if AI helped you find the next useful debugging question?",
            Reason = "Developers need practical, grounded AI workflows.",
            SuggestedHashtags = "#AI #SoftwareEngineering",
            RelevanceScore = 90,
            EngagementPotentialScore = 80,
            BrandFitScore = 85,
            OverallScore = 85,
            CreatedAt = OldTimestamp,
            UpdatedAt = OldTimestamp
        };
        dbContext.ContentIdeas.Add(idea);
        dbContext.SaveChanges();
        return idea;
    }

    private static void SeedContext(AppDbContext dbContext, Guid workspaceId)
    {
        dbContext.BrandProfiles.Add(new BrandProfile
        {
            Id = Guid.NewGuid(),
            WorkspaceId = workspaceId,
            Name = "Example brand",
            Description = "A software brand for practical developer education.",
            Industry = "Software",
            ToneOfVoice = "Direct and practical",
            ContentGoals = "Teach useful engineering lessons"
        });
        dbContext.AudienceProfiles.Add(new AudienceProfile
        {
            Id = Guid.NewGuid(),
            WorkspaceId = workspaceId,
            Description = "Software developers",
            Demographics = "Working engineers",
            Interests = "AI and software engineering",
            PainPoints = "Debugging complex code",
            Goals = "Build better software"
        });
        dbContext.ContentStrategies.Add(new ContentStrategy
        {
            Id = Guid.NewGuid(),
            WorkspaceId = workspaceId,
            PostsPerWeek = 3,
            ContentPillars = "Engineering education",
            ContentObjectives = "Education and authority",
            PreferredFormats = "Practical frameworks",
            StorytellingStyle = "Technical lessons",
            HashtagStrategy = "Relevant niche tags",
            TopicsToPrioritize = "AI for developers",
            TopicsToAvoid = "Unsupported claims",
            PreferredPostingTimes = "Weekdays"
        });
        dbContext.SaveChanges();
    }

    private static ContentPost SeedOlder(
        AppDbContext dbContext,
        Guid contentIdeaId,
        Guid workspaceId,
        ContentPostStatus status,
        string content,
        DateTime createdAt,
        ContentPlatform platform = ContentPlatform.LinkedIn)
    {
        var post = new ContentPost
        {
            Id = Guid.NewGuid(),
            WorkspaceId = workspaceId,
            ContentIdeaId = contentIdeaId,
            Platform = platform,
            Status = status,
            Content = content,
            Hook = "Existing hook",
            CallToAction = string.Empty,
            Hashtags = ["#AI"],
            ContentType = ContentType.Insight,
            StorytellingStructure = "Hook → Insight",
            CreatedAt = createdAt,
            UpdatedAt = createdAt
        };
        dbContext.ContentPosts.Add(post);
        dbContext.SaveChanges();
        return post;
    }

    private static string CreateValidResponse(string platform) => $$"""
        {
          "platform": "{{platform}}",
          "content": "A useful opening\n\nA practical takeaway for developers.",
          "hook": "A useful opening",
          "callToAction": "What would you add?",
          "hashtags": ["AI", "#SoftwareEngineering"],
          "contentType": "Educational",
          "storytellingStructure": "Hook → Insight → Takeaway → CTA"
        }
        """;

    private sealed class StubAiProvider(string response) : IAiProvider
    {
        public int RequestCount { get; private set; }

        public string UserPrompt { get; private set; } = string.Empty;

        public AiResponseSchema? ResponseSchema { get; private set; }

        public Task<string> GenerateAsync(
            string systemPrompt,
            string userPrompt,
            AiResponseSchema responseSchema,
            CancellationToken cancellationToken = default)
        {
            RequestCount++;
            UserPrompt = userPrompt;
            ResponseSchema = responseSchema;
            return Task.FromResult(response);
        }
    }
}
