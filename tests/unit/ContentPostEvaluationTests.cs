using System.Security.Claims;
using Api.Application.AI;
using Api.Application.AI.Prompts;
using Api.Application.Authentication;
using Api.Application.ContentEvaluation;
using Api.Application.ContentIdeas;
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

public sealed class ContentPostEvaluationTests
{
    private static readonly DateTime OldTimestamp = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Evaluation_receives_the_full_workspace_and_idea_context()
    {
        var workspaceId = Guid.NewGuid();
        await using var dbContext = CreateDbContext();
        var idea = AddIdea(dbContext, workspaceId);
        SeedContext(dbContext, workspaceId);
        var post = SeedPost(dbContext, workspaceId, idea.Id, "Draft body");
        var aiProvider = new StubAiProvider(CreateValidEvaluation("90"));
        var service = CreateService(dbContext, workspaceId, aiProvider);

        await service.EvaluateAsync(post.Id);

        Assert.Contains("Example brand", aiProvider.UserPrompt);
        Assert.Contains("Software developers", aiProvider.UserPrompt);
        Assert.Contains("Technical lessons", aiProvider.UserPrompt);
        Assert.Contains("Practical AI debugging", aiProvider.UserPrompt);
        Assert.Contains("Draft body", aiProvider.UserPrompt);
        Assert.Contains("LinkedIn", aiProvider.UserPrompt);
        Assert.Equal(1, aiProvider.RequestCount);
    }

    [Fact]
    public async Task Evaluation_uses_its_own_response_schema_not_the_generation_schema()
    {
        var workspaceId = Guid.NewGuid();
        await using var dbContext = CreateDbContext();
        var idea = AddIdea(dbContext, workspaceId);
        SeedContext(dbContext, workspaceId);
        var post = SeedPost(dbContext, workspaceId, idea.Id, "Draft body");
        var aiProvider = new StubAiProvider(CreateValidEvaluation("90"));
        var service = CreateService(dbContext, workspaceId, aiProvider);

        await service.EvaluateAsync(post.Id);

        Assert.Same(ContentEvaluationResponseSchema.Value, aiProvider.ResponseSchema);
        Assert.NotSame(ContentPostResponseSchema.Value, aiProvider.ResponseSchema);
        Assert.NotSame(ContentIdeaResponseSchema.Value, aiProvider.ResponseSchema);
        Assert.Equal("content_quality_evaluation", aiProvider.ResponseSchema!.Name);
    }

    [Fact]
    public async Task Strong_relevant_content_passes()
    {
        var workspaceId = Guid.NewGuid();
        await using var dbContext = CreateDbContext();
        var idea = AddIdea(dbContext, workspaceId);
        SeedContext(dbContext, workspaceId);
        var post = SeedPost(dbContext, workspaceId, idea.Id, "Draft body");
        var service = CreateService(dbContext, workspaceId, new StubAiProvider(CreateValidEvaluation("88")));

        var result = await service.EvaluateAsync(post.Id);

        Assert.NotNull(result);
        Assert.Equal(post.Id, result.ContentPostId);
        Assert.Equal(88m, result.OverallScore);
        Assert.True(result.Passed);
        Assert.Equal("MinorEdit", result.Recommendation);
        Assert.Equal(70m, result.PassThreshold);
        Assert.Equal(88m, result.Dimensions.StrategicFit);
        Assert.Equal(88m, result.Dimensions.FactualReliability);
        Assert.Equal(88m, result.Dimensions.AudienceFit);
    }

    [Theory]
    [InlineData("40", false, "Rewrite")]
    [InlineData("55", false, "Rewrite")]
    [InlineData("65", false, "Revise")]
    public async Task Poor_quality_is_reported_as_failing_and_is_never_hidden(
        string score,
        bool expectedPass,
        string expectedRecommendation)
    {
        var workspaceId = Guid.NewGuid();
        await using var dbContext = CreateDbContext();
        var idea = AddIdea(dbContext, workspaceId);
        SeedContext(dbContext, workspaceId);
        var post = SeedPost(dbContext, workspaceId, idea.Id, "Draft body");
        var service = CreateService(dbContext, workspaceId, new StubAiProvider(CreateValidEvaluation(score)));

        var result = await service.EvaluateAsync(post.Id);

        Assert.NotNull(result);
        Assert.Equal(expectedPass, result.Passed);
        Assert.Equal(expectedRecommendation, result.Recommendation);
    }

    [Fact]
    public async Task Low_dimension_scores_survive_into_the_response()
    {
        var workspaceId = Guid.NewGuid();
        await using var dbContext = CreateDbContext();
        var idea = AddIdea(dbContext, workspaceId);
        SeedContext(dbContext, workspaceId);
        var post = SeedPost(dbContext, workspaceId, idea.Id, "Draft body");
        var response = CreateValidEvaluation("90")
            .Replace("\"audienceFit\": 90", "\"audienceFit\": 20", StringComparison.Ordinal)
            .Replace("\"factualReliability\": 90", "\"factualReliability\": 15", StringComparison.Ordinal);
        var service = CreateService(dbContext, workspaceId, new StubAiProvider(response));

        var result = await service.EvaluateAsync(post.Id);

        Assert.NotNull(result);
        Assert.Equal(20m, result.Dimensions.AudienceFit);
        Assert.Equal(15m, result.Dimensions.FactualReliability);
        Assert.Equal(75.5m, result.OverallScore);
    }

    [Fact]
    public async Task Evaluation_never_approves_or_otherwise_mutates_the_post()
    {
        var workspaceId = Guid.NewGuid();
        await using var dbContext = CreateDbContext();
        var idea = AddIdea(dbContext, workspaceId);
        SeedContext(dbContext, workspaceId);
        var post = SeedPost(dbContext, workspaceId, idea.Id, "Draft body");
        var service = CreateService(dbContext, workspaceId, new StubAiProvider(CreateValidEvaluation("30")));

        var result = await service.EvaluateAsync(post.Id);

        Assert.NotNull(result);
        Assert.False(result.Passed);
        Assert.Equal(ContentPostStatus.Draft, dbContext.ContentPosts.Single().Status);
        Assert.Equal(OldTimestamp, dbContext.ContentPosts.Single().UpdatedAt);
        Assert.Equal("Draft body", dbContext.ContentPosts.Single().Content);
        AssertNoPendingPostWrites(dbContext);
    }

    [Fact]
    public async Task Evaluation_works_on_an_approved_post_without_changing_its_status()
    {
        var workspaceId = Guid.NewGuid();
        await using var dbContext = CreateDbContext();
        var idea = AddIdea(dbContext, workspaceId);
        SeedContext(dbContext, workspaceId);
        var post = SeedPost(dbContext, workspaceId, idea.Id, "Draft body", ContentPostStatus.Approved);
        var service = CreateService(dbContext, workspaceId, new StubAiProvider(CreateValidEvaluation("90")));

        var result = await service.EvaluateAsync(post.Id);

        Assert.NotNull(result);
        Assert.Equal(ContentPostStatus.Approved, dbContext.ContentPosts.Single().Status);
    }

    [Fact]
    public async Task Malformed_evaluation_json_fails_distinctly_from_generation()
    {
        var workspaceId = Guid.NewGuid();
        await using var dbContext = CreateDbContext();
        var idea = AddIdea(dbContext, workspaceId);
        SeedContext(dbContext, workspaceId);
        var post = SeedPost(dbContext, workspaceId, idea.Id, "Draft body");
        var service = CreateService(dbContext, workspaceId, new StubAiProvider("{ not json"));

        await Assert.ThrowsAsync<ContentEvaluationFailedException>(() =>
            service.EvaluateAsync(post.Id));

        Assert.Equal(ContentPostStatus.Draft, dbContext.ContentPosts.Single().Status);
    }

    [Fact]
    public async Task Evaluation_missing_a_dimension_fails_without_persisting_anything()
    {
        var workspaceId = Guid.NewGuid();
        await using var dbContext = CreateDbContext();
        var idea = AddIdea(dbContext, workspaceId);
        SeedContext(dbContext, workspaceId);
        var post = SeedPost(dbContext, workspaceId, idea.Id, "Draft body");
        var response = CreateValidEvaluation("90")
            .Replace("\"clarity\": 90,", string.Empty, StringComparison.Ordinal);
        var service = CreateService(dbContext, workspaceId, new StubAiProvider(response));

        var exception = await Assert.ThrowsAsync<ContentEvaluationFailedException>(() =>
            service.EvaluateAsync(post.Id));

        Assert.Contains("clarity", exception.Message);
    }

    [Fact]
    public async Task Repetition_is_detected_against_another_post_on_the_same_platform()
    {
        var workspaceId = Guid.NewGuid();
        await using var dbContext = CreateDbContext();
        var idea = AddIdea(dbContext, workspaceId);
        SeedContext(dbContext, workspaceId);
        var duplicate = SeedPost(
            dbContext,
            workspaceId,
            idea.Id,
            "Stop guessing why your deploys fail and measure the cause instead.");
        var post = SeedPost(
            dbContext,
            workspaceId,
            idea.Id,
            "Stop guessing why your deploys fail and measure the cause instead.");
        var service = CreateService(dbContext, workspaceId, new StubAiProvider(CreateValidEvaluation("90")));

        var result = await service.EvaluateAsync(post.Id);

        Assert.NotNull(result);
        Assert.True(result.Repetition.IsRepetitive);
        Assert.Equal(duplicate.Id, result.Repetition.SimilarToContentPostId);
        Assert.Equal(1m, result.Repetition.Similarity);
        Assert.Equal(0.75m, result.Repetition.Threshold);
        Assert.NotEqual(post.Id, result.Repetition.SimilarToContentPostId);
    }

    [Fact]
    public async Task Repetition_ignores_posts_from_other_workspaces()
    {
        var workspaceId = Guid.NewGuid();
        var otherWorkspaceId = Guid.NewGuid();
        await using var dbContext = CreateDbContext();
        var idea = AddIdea(dbContext, workspaceId);
        SeedContext(dbContext, workspaceId);
        SeedPost(
            dbContext,
            otherWorkspaceId,
            idea.Id,
            "Stop guessing why your deploys fail and measure the cause instead.");
        var post = SeedPost(
            dbContext,
            workspaceId,
            idea.Id,
            "Stop guessing why your deploys fail and measure the cause instead.");
        var service = CreateService(dbContext, workspaceId, new StubAiProvider(CreateValidEvaluation("90")));

        var result = await service.EvaluateAsync(post.Id);

        Assert.NotNull(result);
        Assert.False(result.Repetition.IsRepetitive);
        Assert.Null(result.Repetition.SimilarToContentPostId);
    }

    [Fact]
    public async Task Repetition_ignores_posts_on_a_different_platform()
    {
        var workspaceId = Guid.NewGuid();
        await using var dbContext = CreateDbContext();
        var idea = AddIdea(dbContext, workspaceId);
        SeedContext(dbContext, workspaceId);
        SeedPost(
            dbContext,
            workspaceId,
            idea.Id,
            "Stop guessing why your deploys fail and measure the cause instead.",
            ContentPostStatus.Draft,
            ContentPlatform.X);
        var post = SeedPost(dbContext, workspaceId, idea.Id, "Stop guessing why your deploys fail and measure the cause instead.");
        var service = CreateService(dbContext, workspaceId, new StubAiProvider(CreateValidEvaluation("90")));

        var result = await service.EvaluateAsync(post.Id);

        Assert.NotNull(result);
        Assert.False(result.Repetition.IsRepetitive);
    }

    [Fact]
    public async Task Foreign_workspace_post_is_not_found_and_never_calls_the_evaluator()
    {
        var workspaceId = Guid.NewGuid();
        var otherWorkspaceId = Guid.NewGuid();
        await using var dbContext = CreateDbContext();
        var idea = AddIdea(dbContext, otherWorkspaceId);
        SeedContext(dbContext, otherWorkspaceId);
        var foreign = SeedPost(dbContext, otherWorkspaceId, idea.Id, "Theirs");
        var aiProvider = new StubAiProvider(CreateValidEvaluation("90"));
        var service = CreateService(dbContext, workspaceId, aiProvider);

        Assert.Null(await service.EvaluateAsync(foreign.Id));
        Assert.Equal(0, aiProvider.RequestCount);
    }

    [Fact]
    public async Task Unknown_post_is_not_found_and_never_calls_the_evaluator()
    {
        var workspaceId = Guid.NewGuid();
        await using var dbContext = CreateDbContext();
        var aiProvider = new StubAiProvider(CreateValidEvaluation("90"));
        var service = CreateService(dbContext, workspaceId, aiProvider);

        Assert.Null(await service.EvaluateAsync(Guid.NewGuid()));
        Assert.Equal(0, aiProvider.RequestCount);
    }

    [Fact]
    public async Task Evaluation_requires_complete_workspace_context()
    {
        var workspaceId = Guid.NewGuid();
        await using var dbContext = CreateDbContext();
        var idea = AddIdea(dbContext, workspaceId);
        var post = SeedPost(dbContext, workspaceId, idea.Id, "Draft body");
        var aiProvider = new StubAiProvider(CreateValidEvaluation("90"));
        var service = CreateService(dbContext, workspaceId, aiProvider);

        await Assert.ThrowsAsync<ContentIdeaContextIncompleteException>(() =>
            service.EvaluateAsync(post.Id));

        Assert.Equal(0, aiProvider.RequestCount);
    }

    [Fact]
    public async Task Pass_threshold_is_honoured_from_configuration()
    {
        var workspaceId = Guid.NewGuid();
        await using var dbContext = CreateDbContext();
        var idea = AddIdea(dbContext, workspaceId);
        SeedContext(dbContext, workspaceId);
        var post = SeedPost(dbContext, workspaceId, idea.Id, "Draft body");
        var service = CreateService(
            dbContext,
            workspaceId,
            new StubAiProvider(CreateValidEvaluation("75")),
            new ContentQualityOptions { PassThreshold = 80m });

        var result = await service.EvaluateAsync(post.Id);

        Assert.NotNull(result);
        Assert.False(result.Passed);
        Assert.Equal(80m, result.PassThreshold);
    }

    [Fact]
    public async Task Generation_does_not_trigger_evaluation()
    {
        var workspaceId = Guid.NewGuid();
        await using var dbContext = CreateDbContext();
        var idea = AddIdea(dbContext, workspaceId);
        SeedContext(dbContext, workspaceId);
        var aiProvider = new StubAiProvider(CreateValidPostResponse());
        var service = CreateService(dbContext, workspaceId, aiProvider);

        var result = await service.GenerateFromIdeaAsync(
            idea.Id,
            new GenerateContentPostRequest { Platform = ContentPlatform.LinkedIn });

        Assert.NotNull(result);
        Assert.Equal(1, aiProvider.RequestCount);
        Assert.Same(ContentPostResponseSchema.Value, aiProvider.ResponseSchema);
    }

    private static void AssertNoPendingPostWrites(AppDbContext dbContext)
    {
        var pendingWrites = dbContext.ChangeTracker
            .Entries<ContentPost>()
            .Where(entry =>
                entry.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
            .Select(entry => entry.State.ToString())
            .ToArray();

        Assert.Empty(pendingWrites);
    }

    private static AppDbContext CreateDbContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static ContentPostService CreateService(
        AppDbContext dbContext,
        Guid workspaceId,
        StubAiProvider aiProvider,
        ContentQualityOptions? qualityOptions = null) =>
        new(
            dbContext,
            CreateCurrentUser(workspaceId),
            AuditLogTestContext.Create(dbContext, workspaceId),
            aiProvider,
            AiRunTestContext.Create(dbContext, workspaceId),
            new ContentPostPromptBuilder(),
            new ContentEvaluationPromptBuilder(),
            new ContentRepetitionDetector(),
            new NotSchedulingScheduledPostStore(),
            NullLogger<ContentPostService>.Instance,
            Options.Create(new AiOptions
            {
                Provider = "Gemini",
                Model = "test-model",
                ApiKey = "test-placeholder"
            }),
            Options.Create(qualityOptions ?? new ContentQualityOptions()),
            Options.Create(new SchedulingOptions()));

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

    private static ContentPost SeedPost(
        AppDbContext dbContext,
        Guid workspaceId,
        Guid contentIdeaId,
        string content,
        ContentPostStatus status = ContentPostStatus.Draft,
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
            Hook = "An existing hook",
            CallToAction = string.Empty,
            Hashtags = ["#AI"],
            ContentType = ContentType.Insight,
            StorytellingStructure = "Hook → Insight",
            CreatedAt = OldTimestamp,
            UpdatedAt = OldTimestamp
        };
        dbContext.ContentPosts.Add(post);
        dbContext.SaveChanges();
        return post;
    }

    private static string CreateValidEvaluation(string score) => $$"""
        {
          "strategicFit": {{score}},
          "audienceFit": {{score}},
          "hookStrength": {{score}},
          "value": {{score}},
          "clarity": {{score}},
          "originality": {{score}},
          "storytelling": {{score}},
          "platformFit": {{score}},
          "voiceMatch": {{score}},
          "factualReliability": {{score}},
          "strengths": ["Specific to the audience"],
          "issues": []
        }
        """;

    private static string CreateValidPostResponse() => """
        {
          "platform": "LinkedIn",
          "content": "A useful opening\n\nA practical takeaway for developers.",
          "hook": "A useful opening",
          "callToAction": "What would you add?",
          "hashtags": ["AI"],
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
