using System.Security.Claims;
using Api.Application.AI;
using Api.Application.AI.Prompts;
using Api.Application.Authentication;
using Api.Application.ContentIdeas;
using Api.Application.ContentPosts;
using Api.Application.Services;
using Api.Contracts.ContentPost;
using Api.Domain.Entities;
using Api.Domain.Enums;
using Api.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Api.Configuration;
using Xunit;

namespace Api.Tests;

public sealed class ContentPostServiceTests
{
    [Theory]
    [InlineData(ContentPlatform.LinkedIn, "LinkedIn")]
    [InlineData(ContentPlatform.X, "X")]
    public async Task Approved_idea_generates_and_persists_one_draft_with_context(
        ContentPlatform platform,
        string platformName)
    {
        var workspaceId = Guid.NewGuid();
        await using var dbContext = CreateDbContext();
        var idea = CreateIdea(workspaceId, ContentIdeaStatus.Approved);
        SeedContext(dbContext, workspaceId, idea);
        var aiProvider = new StubAiProvider(CreateValidResponse(platformName));
        var service = CreateService(dbContext, workspaceId, aiProvider);

        var result = await service.GenerateFromIdeaAsync(
            idea.Id,
            new GenerateContentPostRequest { Platform = platform });

        Assert.NotNull(result);
        Assert.Equal(idea.Id, result.ContentIdeaId);
        Assert.Equal(platformName, result.Platform);
        Assert.Equal("Draft", result.Status);
        Assert.Equal("Educational", result.ContentType);
        Assert.Equal(["#AI", "#SoftwareEngineering"], result.Hashtags);
        Assert.Equal(1, aiProvider.RequestCount);
        Assert.Same(ContentPostResponseSchema.Value, aiProvider.ResponseSchema);
        Assert.Contains("Example brand", aiProvider.UserPrompt);
        Assert.Contains("Software developers", aiProvider.UserPrompt);
        Assert.Contains("Technical lessons", aiProvider.UserPrompt);
        Assert.Contains("A practical debugging framework", aiProvider.UserPrompt);
        Assert.Contains(platformName, aiProvider.UserPrompt);
        Assert.Single(dbContext.ContentPosts);
        Assert.Equal(workspaceId, dbContext.ContentPosts.Single().WorkspaceId);
    }

    [Theory]
    [InlineData(ContentIdeaStatus.Suggested)]
    [InlineData(ContentIdeaStatus.Rejected)]
    public async Task Non_approved_idea_cannot_generate_post(ContentIdeaStatus status)
    {
        var workspaceId = Guid.NewGuid();
        await using var dbContext = CreateDbContext();
        var idea = CreateIdea(workspaceId, status);
        SeedContext(dbContext, workspaceId, idea);
        var aiProvider = new StubAiProvider(CreateValidResponse("LinkedIn"));
        var service = CreateService(dbContext, workspaceId, aiProvider);

        await Assert.ThrowsAsync<ContentIdeaInvalidStatusException>(() =>
            service.GenerateFromIdeaAsync(
                idea.Id,
                new GenerateContentPostRequest { Platform = ContentPlatform.LinkedIn }));

        Assert.Equal(0, aiProvider.RequestCount);
        Assert.Empty(dbContext.ContentPosts);
    }

    [Fact]
    public async Task Foreign_workspace_idea_is_not_found_and_does_not_call_ai()
    {
        var currentWorkspaceId = Guid.NewGuid();
        var foreignWorkspaceId = Guid.NewGuid();
        await using var dbContext = CreateDbContext();
        var foreignIdea = CreateIdea(foreignWorkspaceId, ContentIdeaStatus.Approved);
        SeedContext(dbContext, foreignWorkspaceId, foreignIdea);
        var aiProvider = new StubAiProvider(CreateValidResponse("LinkedIn"));
        var service = CreateService(dbContext, currentWorkspaceId, aiProvider);

        var result = await service.GenerateFromIdeaAsync(
            foreignIdea.Id,
            new GenerateContentPostRequest { Platform = ContentPlatform.LinkedIn });

        Assert.Null(result);
        Assert.Equal(0, aiProvider.RequestCount);
        Assert.Empty(dbContext.ContentPosts);
    }

    [Fact]
    public async Task Existing_draft_is_returned_without_another_ai_request()
    {
        var workspaceId = Guid.NewGuid();
        await using var dbContext = CreateDbContext();
        var idea = CreateIdea(workspaceId, ContentIdeaStatus.Approved);
        SeedContext(dbContext, workspaceId, idea);
        var existing = new ContentPost
        {
            Id = Guid.NewGuid(),
            WorkspaceId = workspaceId,
            ContentIdeaId = idea.Id,
            Platform = ContentPlatform.LinkedIn,
            Status = ContentPostStatus.Draft,
            Content = "Already generated draft",
            Hook = "Existing hook",
            CallToAction = string.Empty,
            Hashtags = ["#AI"],
            ContentType = ContentType.Insight,
            StorytellingStructure = "Hook → Insight",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        dbContext.ContentPosts.Add(existing);
        await dbContext.SaveChangesAsync();
        var aiProvider = new StubAiProvider(CreateValidResponse("LinkedIn"));
        var service = CreateService(dbContext, workspaceId, aiProvider);

        var result = await service.GenerateFromIdeaAsync(
            idea.Id,
            new GenerateContentPostRequest { Platform = ContentPlatform.LinkedIn });

        Assert.NotNull(result);
        Assert.Equal(existing.Id, result.Id);
        Assert.Equal("Already generated draft", result.Content);
        Assert.Equal(0, aiProvider.RequestCount);
        Assert.Single(dbContext.ContentPosts);
    }

    [Fact]
    public async Task Malformed_ai_json_is_rejected_and_not_persisted()
    {
        var workspaceId = Guid.NewGuid();
        await using var dbContext = CreateDbContext();
        var idea = CreateIdea(workspaceId, ContentIdeaStatus.Approved);
        SeedContext(dbContext, workspaceId, idea);
        var service = CreateService(
            dbContext,
            workspaceId,
            new StubAiProvider("{"));

        await Assert.ThrowsAsync<InvalidAiResponseException>(() =>
            service.GenerateFromIdeaAsync(
                idea.Id,
                new GenerateContentPostRequest { Platform = ContentPlatform.LinkedIn }));

        Assert.Empty(dbContext.ContentPosts);
    }

    [Fact]
    public async Task Invalid_platform_is_rejected_before_ai_call()
    {
        var workspaceId = Guid.NewGuid();
        await using var dbContext = CreateDbContext();
        var idea = CreateIdea(workspaceId, ContentIdeaStatus.Approved);
        SeedContext(dbContext, workspaceId, idea);
        var aiProvider = new StubAiProvider(CreateValidResponse("LinkedIn"));
        var service = CreateService(dbContext, workspaceId, aiProvider);

        await Assert.ThrowsAsync<InvalidContentPostRequestException>(() =>
            service.GenerateFromIdeaAsync(
                idea.Id,
                new GenerateContentPostRequest { Platform = (ContentPlatform)999 }));

        Assert.Equal(0, aiProvider.RequestCount);
    }

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

    private static void SeedContext(
        AppDbContext dbContext,
        Guid workspaceId,
        ContentIdea idea)
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
        dbContext.ContentIdeas.Add(idea);
        dbContext.SaveChanges();
    }

    private static ContentIdea CreateIdea(Guid workspaceId, ContentIdeaStatus status) => new()
    {
        Id = Guid.NewGuid(),
        WorkspaceId = workspaceId,
        Status = status,
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
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    };

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

        public string SystemPrompt { get; private set; } = string.Empty;

        public string UserPrompt { get; private set; } = string.Empty;

        public AiResponseSchema? ResponseSchema { get; private set; }

        public Task<string> GenerateAsync(
            string systemPrompt,
            string userPrompt,
            AiResponseSchema responseSchema,
            CancellationToken cancellationToken = default)
        {
            RequestCount++;
            SystemPrompt = systemPrompt;
            UserPrompt = userPrompt;
            ResponseSchema = responseSchema;
            return Task.FromResult(response);
        }
    }
}