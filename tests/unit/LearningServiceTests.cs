using Api.Application.AI;
using Api.Application.AI.Prompts;
using Api.Application.Analytics;
using Api.Application.Authentication;
using Api.Application.Learning;
using Api.Configuration;
using Api.Domain.Entities;
using Api.Domain.Enums;
using Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Api.Tests;

/// <summary>
/// Covers the loop end to end over an in-memory database: what it reads, what it
/// asks the model, and what it stores.
/// </summary>
public sealed class LearningServiceTests
{
    private static readonly DateTime Anchor = new(2026, 9, 1, 12, 0, 0, DateTimeKind.Utc);

    private const string ValidRecommendation = "{\"recommendation\":\"Try one more Automation post next week.\"}";

    // ---------------------------------------------------------------------
    // Workspace isolation
    // ---------------------------------------------------------------------

    [Fact]
    public async Task A_run_never_analyses_another_workspaces_posts()
    {
        await using var dbContext = CreateDbContext();
        var workspaceId = Guid.NewGuid();

        // This workspace's own posts are all on one platform, so there is nothing
        // to compare. The other workspace has a clear difference that would
        // produce a signal if it leaked in.
        for (var index = 0; index < 6; index++)
        {
            SeedWithMetrics(dbContext, workspaceId, ContentPlatform.LinkedIn, 8m);
        }

        for (var index = 0; index < 3; index++)
        {
            SeedWithMetrics(dbContext, workspaceId, ContentPlatform.LinkedIn, 8m);
            SeedWithMetrics(dbContext, Guid.NewGuid(), ContentPlatform.LinkedIn, 8m);
            SeedWithMetrics(dbContext, Guid.NewGuid(), ContentPlatform.X, 0.5m);
        }

        var result = await CreateService(dbContext, workspaceId).GenerateInsightsAsync();

        Assert.Equal(9, result.MeasuredPosts);
        Assert.Empty(result.Insights);
    }

    [Fact]
    public async Task Stored_signals_are_never_returned_to_another_workspace()
    {
        await using var dbContext = CreateDbContext();
        var workspaceId = Guid.NewGuid();

        SeedSignal(dbContext, workspaceId, "PLATFORM_ENGAGEMENT", Anchor);
        SeedSignal(dbContext, Guid.NewGuid(), "PLATFORM_ENGAGEMENT", Anchor);

        var result = await CreateService(dbContext, workspaceId).GetInsightsAsync(50);

        Assert.Single(result);
    }

    // ---------------------------------------------------------------------
    // Insufficient data
    // ---------------------------------------------------------------------

    [Fact]
    public async Task A_run_with_too_few_measured_posts_makes_no_ai_call_and_stores_nothing()
    {
        await using var dbContext = CreateDbContext();
        var workspaceId = Guid.NewGuid();

        // Four measured posts, and a fifth that was published but never measured.
        SeedWithMetrics(dbContext, workspaceId, ContentPlatform.LinkedIn, 8m);
        SeedWithMetrics(dbContext, workspaceId, ContentPlatform.LinkedIn, 8m);
        SeedWithMetrics(dbContext, workspaceId, ContentPlatform.X, 1m);
        SeedWithMetrics(dbContext, workspaceId, ContentPlatform.X, 1m);
        SeedPublishedPost(dbContext, workspaceId, Anchor);

        var provider = new RecordingAiProvider(ValidRecommendation);
        var result = await CreateService(dbContext, workspaceId, provider).GenerateInsightsAsync();

        Assert.False(result.HasSufficientData);
        Assert.Equal(5, result.PostsInWindow);
        Assert.Equal(4, result.MeasuredPosts);
        Assert.Equal(5, result.MinimumMeasuredPosts);
        Assert.Empty(result.Insights);
        Assert.Empty(provider.Prompts);
        Assert.Empty(await dbContext.LearningSignals.ToListAsync());
        Assert.Empty(await dbContext.AiRuns.ToListAsync());
    }

    [Fact]
    public async Task A_run_with_nothing_published_reports_zero_rather_than_failing()
    {
        await using var dbContext = CreateDbContext();
        var provider = new RecordingAiProvider(ValidRecommendation);

        var result = await CreateService(dbContext, Guid.NewGuid(), provider).GenerateInsightsAsync();

        Assert.False(result.HasSufficientData);
        Assert.Equal(0, result.PostsInWindow);
        Assert.Equal(0, result.MeasuredPosts);
        Assert.Empty(provider.Prompts);
    }

    // ---------------------------------------------------------------------
    // Storing a signal
    // ---------------------------------------------------------------------

    [Fact]
    public async Task A_signal_stores_the_measured_observation_and_the_inferred_recommendation_separately()
    {
        await using var dbContext = CreateDbContext();
        var workspaceId = Guid.NewGuid();
        SeedClearDifference(dbContext, workspaceId);

        var result = await CreateService(dbContext, workspaceId).GenerateInsightsAsync();

        Assert.True(result.HasSufficientData);

        var signal = Assert.Single(await dbContext.LearningSignals.ToListAsync());
        var insight = Assert.Single(result.Insights);

        // The observation is written by code from the metric totals.
        Assert.Equal(
            "Across 5 measured posts, Platform LinkedIn averaged 6% engagement over 3 posts, " +
            "compared with 2% for X over 2 posts.",
            signal.Signal);
        Assert.DoesNotContain("Try one more", signal.Signal);

        // The recommendation is the model's, and it is not the observation.
        Assert.Equal("Try one more Automation post next week.", insight.Recommendation);
        Assert.NotEqual(signal.Signal, insight.Recommendation);

        // The inference lives in metadata, in its own field, alongside the
        // evidence it was drawn from.
        Assert.Contains("\"recommendation\":\"Try one more Automation post next week.\"", signal.Metadata);
        Assert.Contains("\"dimension\":\"Platform\"", signal.Metadata);
        Assert.Contains("\"best\"", signal.Metadata);
        Assert.Contains("\"worst\"", signal.Metadata);
    }

    [Fact]
    public async Task A_signal_records_its_own_workspace_and_leaves_source_id_empty()
    {
        await using var dbContext = CreateDbContext();
        var workspaceId = Guid.NewGuid();
        SeedClearDifference(dbContext, workspaceId);

        await CreateService(dbContext, workspaceId).GenerateInsightsAsync();

        var signal = Assert.Single(await dbContext.LearningSignals.ToListAsync());
        Assert.Equal(workspaceId, signal.WorkspaceId);
        Assert.Equal("PerformanceAnalysis", signal.SourceType);

        // A platform comparison is measured over every post in the window, so
        // pointing at one post would misstate where the evidence came from.
        Assert.Null(signal.SourceId);
    }

    [Fact]
    public async Task A_signal_carries_the_evidence_strength_the_analyzer_computed()
    {
        await using var dbContext = CreateDbContext();
        var workspaceId = Guid.NewGuid();
        SeedClearDifference(dbContext, workspaceId);

        var result = await CreateService(dbContext, workspaceId).GenerateInsightsAsync();

        var insight = Assert.Single(result.Insights);
        Assert.Equal(5, insight.EvidenceCount);
        Assert.Equal(50m, insight.Confidence);
    }

    [Fact]
    public async Task Insights_are_read_back_newest_first()
    {
        await using var dbContext = CreateDbContext();
        var workspaceId = Guid.NewGuid();

        SeedSignal(dbContext, workspaceId, "PLATFORM_ENGAGEMENT", Anchor);
        SeedSignal(dbContext, workspaceId, "CONTENT_TYPE_ENGAGEMENT", Anchor.AddDays(1));
        SeedSignal(dbContext, workspaceId, "CONTENT_PILLAR_ENGAGEMENT", Anchor.AddDays(2));

        var result = await CreateService(dbContext, workspaceId).GetInsightsAsync(50);

        Assert.Equal(
            ["CONTENT_PILLAR_ENGAGEMENT", "CONTENT_TYPE_ENGAGEMENT", "PLATFORM_ENGAGEMENT"],
            result.Select(insight => insight.Type).ToArray());
    }

    [Fact]
    public async Task A_read_is_capped_rather_than_returning_every_stored_signal()
    {
        await using var dbContext = CreateDbContext();
        var workspaceId = Guid.NewGuid();

        dbContext.LearningSignals.AddRange(Enumerable.Range(0, 205).Select(index =>
            new LearningSignal
            {
                Id = Guid.NewGuid(),
                WorkspaceId = workspaceId,
                SourceType = "PerformanceAnalysis",
                SignalType = "PLATFORM_ENGAGEMENT",
                Signal = "An observation",
                Confidence = 50m,
                CreatedAt = Anchor.AddDays(index)
            }));
        dbContext.SaveChanges();

        var service = CreateService(dbContext, workspaceId);

        Assert.Equal(2, (await service.GetInsightsAsync(2)).Count);
        Assert.Equal(LearningService.MaxInsightsLimit, (await service.GetInsightsAsync(10_000)).Count);

        // A nonsensical page size is pulled into range rather than returning
        // nothing or everything.
        Assert.Single(await service.GetInsightsAsync(0));
        Assert.Single(await service.GetInsightsAsync(-5));
    }

    [Fact]
    public async Task Signals_survive_a_signal_whose_metadata_cannot_be_read()
    {
        await using var dbContext = CreateDbContext();
        var workspaceId = Guid.NewGuid();

        SeedSignal(dbContext, workspaceId, "PLATFORM_ENGAGEMENT", Anchor, metadata: "{ not json");
        SeedSignal(dbContext, workspaceId, "CONTENT_TYPE_ENGAGEMENT", Anchor.AddDays(1));

        var result = await CreateService(dbContext, workspaceId).GetInsightsAsync(50);

        // The measurement on the broken row is intact, so it is still returned.
        // Failing the whole read would hide real signals over a detail problem.
        Assert.Equal(2, result.Count);

        var broken = result.Single(insight => insight.Type == "PLATFORM_ENGAGEMENT");
        Assert.Equal("An observation about measured performance", broken.Observation);
        Assert.Equal(50m, broken.Confidence);
        Assert.Equal(string.Empty, broken.Dimension);
        Assert.Equal(0, broken.EvidenceCount);
        Assert.Null(broken.Recommendation);
    }

    // ---------------------------------------------------------------------
    // Determinism
    // ---------------------------------------------------------------------

    [Fact]
    public async Task Two_runs_over_the_same_measurements_write_the_same_observation()
    {
        await using var firstContext = CreateDbContext();
        await using var secondContext = CreateDbContext();
        var workspaceId = Guid.NewGuid();

        SeedClearDifference(firstContext, workspaceId);
        SeedClearDifference(secondContext, workspaceId);

        var first = await CreateService(firstContext, workspaceId).GenerateInsightsAsync();
        var second = await CreateService(secondContext, workspaceId).GenerateInsightsAsync();

        Assert.Equal(
            first.Insights.Select(insight => (insight.Type, insight.Observation, insight.Confidence)),
            second.Insights.Select(insight => (insight.Type, insight.Observation, insight.Confidence)));
    }

    [Fact]
    public async Task A_repeat_run_appends_rather_than_replacing_earlier_signals()
    {
        await using var dbContext = CreateDbContext();
        var workspaceId = Guid.NewGuid();
        SeedClearDifference(dbContext, workspaceId);

        var service = CreateService(dbContext, workspaceId);
        await service.GenerateInsightsAsync();
        await service.GenerateInsightsAsync();

        // A later run over a longer history is a new observation, not a
        // correction of the old one, so the earlier row is left alone.
        Assert.Equal(2, await dbContext.LearningSignals.CountAsync());
    }

    // ---------------------------------------------------------------------
    // What the model is asked
    // ---------------------------------------------------------------------

    [Fact]
    public async Task The_prompt_carries_the_measured_evidence_and_the_workspace_context()
    {
        await using var dbContext = CreateDbContext();
        var workspaceId = Guid.NewGuid();
        SeedClearDifference(dbContext, workspaceId);
        SeedBrandProfile(dbContext, workspaceId, "Mayor X");

        var provider = new RecordingAiProvider(ValidRecommendation);
        await CreateService(dbContext, workspaceId, provider).GenerateInsightsAsync();

        var prompt = Assert.Single(provider.Prompts);

        Assert.Equal(LearningInsightSystemPrompt.Value, provider.SystemPrompts.Single());
        Assert.Contains("LinkedIn", prompt);
        Assert.Contains("\"observation\"", prompt);
        Assert.Contains("\"engagementRatePercent\"", prompt);
        Assert.Contains("Mayor X", prompt);
    }

    [Fact]
    public async Task A_missing_brand_profile_is_sent_as_absent_rather_than_invented()
    {
        await using var dbContext = CreateDbContext();
        var workspaceId = Guid.NewGuid();
        SeedClearDifference(dbContext, workspaceId);

        var provider = new RecordingAiProvider(ValidRecommendation);
        await CreateService(dbContext, workspaceId, provider).GenerateInsightsAsync();

        var prompt = Assert.Single(provider.Prompts);

        Assert.Contains("\"brand\":null", prompt);
        Assert.Contains("\"audience\":null", prompt);
        Assert.Contains("\"contentStrategy\":null", prompt);
    }

    [Fact]
    public async Task A_successful_recommendation_is_recorded_as_an_ai_run()
    {
        await using var dbContext = CreateDbContext();
        var workspaceId = Guid.NewGuid();
        SeedClearDifference(dbContext, workspaceId);

        await CreateService(dbContext, workspaceId).GenerateInsightsAsync();

        var run = Assert.Single(await dbContext.AiRuns.ToListAsync());
        Assert.Equal(workspaceId, run.WorkspaceId);
        Assert.Equal(AIRunStatus.Succeeded, run.Status);
        Assert.Equal(LearningInsightSystemPrompt.Version, run.PromptVersion);
        Assert.Equal("PERFORMANCE_ANALYSIS", AIRunTypeStorage.ToStorage(run.RunType));
    }

    [Fact]
    public async Task A_provider_failure_stores_no_signal_and_is_recorded_as_a_failed_run()
    {
        await using var dbContext = CreateDbContext();
        var workspaceId = Guid.NewGuid();
        SeedClearDifference(dbContext, workspaceId);

        var provider = new RecordingAiProvider(
            new InvalidOperationException("provider is down"));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => CreateService(dbContext, workspaceId, provider).GenerateInsightsAsync());

        Assert.Empty(await dbContext.LearningSignals.ToListAsync());

        var run = Assert.Single(await dbContext.AiRuns.ToListAsync());
        Assert.Equal(AIRunStatus.Failed, run.Status);
        Assert.Equal("PERFORMANCE_ANALYSIS", AIRunTypeStorage.ToStorage(run.RunType));
        Assert.Equal(LearningInsightSystemPrompt.Version, run.PromptVersion);
    }

    [Fact]
    public async Task A_run_with_a_finding_the_model_cannot_answer_stores_nothing()
    {
        await using var dbContext = CreateDbContext();
        var workspaceId = Guid.NewGuid();
        SeedClearDifference(dbContext, workspaceId);

        // Missing the required field, so the answer is unusable however it is
        // worded. Storing the measurement with an empty recommendation would
        // leave a signal that reads as if it had been interpreted.
        var provider = new RecordingAiProvider("{\"summary\":\"looks good\"}");

        await Assert.ThrowsAsync<LearningInsightFailedException>(
            () => CreateService(dbContext, workspaceId, provider).GenerateInsightsAsync());

        Assert.Empty(await dbContext.LearningSignals.ToListAsync());
        Assert.Equal(AIRunStatus.Failed, Assert.Single(await dbContext.AiRuns.ToListAsync()).Status);
    }

    [Fact]
    public async Task A_later_provider_failure_discards_the_earlier_recommendations()
    {
        await using var dbContext = CreateDbContext();
        var workspaceId = Guid.NewGuid();
        SeedTwoDimensionalDifference(dbContext, workspaceId);

        // Succeeds on the first finding, fails on the second. The first signal
        // must not survive: a run either explains every finding it found or
        // stores none of them, otherwise a half-answered run looks complete.
        var provider = new FailOnNthCallAiProvider(2, new InvalidOperationException("provider is down"));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => CreateService(dbContext, workspaceId, provider).GenerateInsightsAsync());

        Assert.Empty(await dbContext.LearningSignals.ToListAsync());

        // The failed attempt is still auditable, and the successful one is kept.
        Assert.Contains(await dbContext.AiRuns.ToListAsync(), run => run.Status == AIRunStatus.Failed);
        Assert.Contains(await dbContext.AiRuns.ToListAsync(), run => run.Status == AIRunStatus.Succeeded);
    }

    // ---------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------

    private static AppDbContext CreateDbContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static LearningService CreateService(
        AppDbContext dbContext,
        Guid workspaceId,
        IAiProvider? provider = null) =>
        new(
            dbContext,
            new CurrentUserService(AuditLogTestContext.CreateAccessor(workspaceId)),
            new PublishedPostMetricsReader(dbContext),
            provider ?? new RecordingAiProvider(ValidRecommendation),
            AiRunTestContext.Create(dbContext, workspaceId),
            new LearningInsightPromptBuilder(),
            Options.Create(new LearningLoopOptions
            {
                LookbackDays = 365,
                MinimumMeasuredPosts = 5,
                MinimumPostsPerGroup = 2,
                MinimumEngagementRateGap = 0.25m
            }),
            Options.Create(new AiOptions
            {
                Provider = "Gemini",
                Model = "test-model",
                ApiKey = "test-placeholder"
            }),
            NullLogger<LearningService>.Instance);

    /// <summary>
    /// Three LinkedIn posts at 6% and two X posts at 2%, all published inside the
    /// lookback window.
    /// </summary>
    private static void SeedClearDifference(AppDbContext dbContext, Guid workspaceId)
    {
        for (var index = 0; index < 3; index++)
        {
            SeedWithMetrics(dbContext, workspaceId, ContentPlatform.LinkedIn, 6m);
        }

        for (var index = 0; index < 2; index++)
        {
            SeedWithMetrics(dbContext, workspaceId, ContentPlatform.X, 2m);
        }
    }

    /// <summary>
    /// Two findings, one per dimension: a platform gap (LinkedIn 6% against
    /// X 2%) and a posting-day gap, because the two groups are published on
    /// different days. Content type is constant and no post has a pillar, so
    /// neither dimension produces a third finding.
    /// </summary>
    private static void SeedTwoDimensionalDifference(AppDbContext dbContext, Guid workspaceId)
    {
        var monday = Anchor;
        var tuesday = Anchor.AddDays(1);

        for (var index = 0; index < 3; index++)
        {
            SeedWithMetricsOn(dbContext, workspaceId, ContentPlatform.LinkedIn, 6m, monday);
        }

        for (var index = 0; index < 2; index++)
        {
            SeedWithMetricsOn(dbContext, workspaceId, ContentPlatform.X, 2m, tuesday);
        }
    }

    private static ContentPost SeedWithMetrics(
        AppDbContext dbContext,
        Guid workspaceId,
        ContentPlatform platform,
        decimal engagementRatePercent) =>
        SeedWithMetricsOn(dbContext, workspaceId, platform, engagementRatePercent, Anchor);

    private static ContentPost SeedWithMetricsOn(
        AppDbContext dbContext,
        Guid workspaceId,
        ContentPlatform platform,
        decimal engagementRatePercent,
        DateTime publishedAt)
    {
        const long reach = 1_000;

        var post = SeedPublishedPost(dbContext, workspaceId, publishedAt, platform);

        dbContext.ContentPostMetrics.Add(new ContentPostMetric
        {
            Id = Guid.NewGuid(),
            WorkspaceId = workspaceId,
            ContentPostId = post.Id,
            Impressions = reach,
            Likes = (long)Math.Round(reach * engagementRatePercent / 100m, MidpointRounding.AwayFromZero),
            Comments = 0,
            Shares = 0,
            RecordedAt = Anchor,
            CreatedAt = Anchor
        });

        dbContext.SaveChanges();
        return post;
    }

    private static ContentPost SeedPublishedPost(
        AppDbContext dbContext,
        Guid workspaceId,
        DateTime publishedAt,
        ContentPlatform platform = ContentPlatform.LinkedIn)
    {
        var idea = new ContentIdea
        {
            Id = Guid.NewGuid(),
            WorkspaceId = workspaceId,
            Platform = platform,
            Title = "An idea",
            Topic = "Automation",
            Angle = "Where the time goes",
            Hook = "Most posts are not the problem",
            Reason = "Measured evidence for the learning loop",
            SuggestedHashtags = "#AI",
            RelevanceScore = 90,
            EngagementPotentialScore = 80,
            BrandFitScore = 85,
            OverallScore = 85,
            CreatedAt = Anchor,
            UpdatedAt = Anchor
        };

        dbContext.ContentIdeas.Add(idea);

        var post = new ContentPost
        {
            Id = Guid.NewGuid(),
            WorkspaceId = workspaceId,
            ContentIdeaId = idea.Id,
            Platform = platform,
            Status = ContentPostStatus.Published,
            Content = "A published post",
            Hook = "Existing hook",
            CallToAction = string.Empty,
            Hashtags = ["#AI"],
            ContentType = ContentType.Insight,
            StorytellingStructure = "Hook → Insight",
            PublishedAt = publishedAt,
            CreatedAt = publishedAt,
            UpdatedAt = publishedAt
        };

        dbContext.ContentPosts.Add(post);
        dbContext.SaveChanges();
        return post;
    }

    private static void SeedBrandProfile(AppDbContext dbContext, Guid workspaceId, string name)
    {
        dbContext.BrandProfiles.Add(new BrandProfile
        {
            Id = Guid.NewGuid(),
            WorkspaceId = workspaceId,
            Name = name,
            Description = "Automation for working founders",
            Industry = "Software",
            ToneOfVoice = "Direct",
            ContentGoals = "Explain things clearly",
            CreatedAt = Anchor,
            UpdatedAt = Anchor
        });

        dbContext.SaveChanges();
    }

    private static LearningSignal SeedSignal(
        AppDbContext dbContext,
        Guid workspaceId,
        string signalType,
        DateTime createdAt,
        string? metadata = null)
    {
        var signal = new LearningSignal
        {
            Id = Guid.NewGuid(),
            WorkspaceId = workspaceId,
            SourceType = "PerformanceAnalysis",
            SignalType = signalType,
            Signal = "An observation about measured performance",
            Confidence = 50m,
            Metadata = metadata ?? System.Text.Json.JsonSerializer.Serialize(new
            {
                dimension = "Platform",
                evidenceCount = 5,
                recommendation = "Try one more Automation post next week."
            }),
            CreatedAt = createdAt
        };

        dbContext.LearningSignals.Add(signal);
        dbContext.SaveChanges();
        return signal;
    }

    /// <summary>
    /// Records every prompt it is given, so a test can assert what the model was
    /// actually asked rather than that a call happened.
    /// </summary>
    private sealed class RecordingAiProvider : IAiProvider
    {
        private readonly object? _response;

        public RecordingAiProvider(string response) => _response = response;

        public RecordingAiProvider(Exception exception) => _response = exception;

        public List<string> SystemPrompts { get; } = [];

        public List<string> Prompts { get; } = [];

        public Task<string> GenerateAsync(
            string systemPrompt,
            string userPrompt,
            AiResponseSchema responseSchema,
            CancellationToken cancellationToken = default)
        {
            SystemPrompts.Add(systemPrompt);
            Prompts.Add(userPrompt);

            return _response is Exception exception
                ? Task.FromException<string>(exception)
                : Task.FromResult((string)_response!);
        }
    }

    /// <summary>
    /// Answers normally until the given call number, then throws. Used to prove
    /// that a mid-run failure does not leave the earlier findings behind.
    /// </summary>
    private sealed class FailOnNthCallAiProvider(int failOnCall, Exception exception) : IAiProvider
    {
        private int _calls;

        public Task<string> GenerateAsync(
            string systemPrompt,
            string userPrompt,
            AiResponseSchema responseSchema,
            CancellationToken cancellationToken = default)
        {
            _calls++;

            return _calls == failOnCall
                ? Task.FromException<string>(exception)
                : Task.FromResult(ValidRecommendation);
        }
    }
}
