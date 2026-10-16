using System.Text.Json;
using Api.Application.AI;
using Api.Application.AI.Prompts;
using Api.Application.Authentication;
using Api.Application.ContentEvaluation;
using Api.Application.ContentIdeas;
using Api.Application.Services;
using Api.Configuration;
using Api.Contracts.ContentIdea;
using Api.Contracts.ContentPost;
using Api.Domain.Entities;
using Api.Domain.Enums;
using Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Api.Tests;

[Collection(ApplicationMetricsCollection.Name)]
public sealed class AiRunTests
{
    private const string ValidIdeaResponse = """
        {"ideas":[{"title":"A useful idea","topic":"Engineering",
        "angle":"Practical lessons","hook":"Start here","reason":"Relevant",
        "hashtags":["#engineering"],"scores":{"relevance":80,
        "engagementPotential":70,"brandFit":90}}]}
        """;

    private const string ValidPostResponse = """
        {"platform":"LinkedIn","hook":"A strong opening","content":"Body copy.",
        "callToAction":"Try it","hashtags":["#engineering"],
        "storytellingStructure":"Problem, fix, outcome","contentType":"Educational"}
        """;

    private const string ValidEvaluationResponse = """
        {"strategicFit":80,"audienceFit":80,"hookStrength":80,"value":80,
        "clarity":80,"originality":80,"storytelling":80,"platformFit":80,
        "voiceMatch":80,"factualReliability":80,"strengths":["Clear"],"issues":[]}
        """;

    [Fact]
    public async Task Generating_ideas_records_a_succeeded_idea_generation_run()
    {
        await using var dbContext = CreateDbContext();
        var workspaceId = await SeedGenerationContextAsync(dbContext);
        var service = CreateIdeaService(
            dbContext, workspaceId, new StubAiProvider(ValidIdeaResponse));

        await service.GenerateAsync(new GenerateContentIdeasRequest
        {
            Platform = ContentPlatform.LinkedIn,
            Count = 1
        });

        var run = Assert.Single(dbContext.AiRuns);
        Assert.Equal(AIRunType.IdeaGeneration, run.RunType);
        Assert.Equal(AIRunStatus.Succeeded, run.Status);
        Assert.Equal(workspaceId, run.WorkspaceId);
    }

    [Fact]
    public async Task A_run_records_the_configured_provider_and_model()
    {
        await using var dbContext = CreateDbContext();
        var workspaceId = await SeedGenerationContextAsync(dbContext);
        var service = CreateIdeaService(
            dbContext, workspaceId, new StubAiProvider(ValidIdeaResponse));

        await service.GenerateAsync(new GenerateContentIdeasRequest
        {
            Platform = ContentPlatform.LinkedIn,
            Count = 1
        });

        var run = Assert.Single(dbContext.AiRuns);
        Assert.Equal("Gemini", run.Provider);
        Assert.Equal("test-model", run.Model);
    }

    [Fact]
    public async Task A_run_records_the_version_of_the_prompt_that_was_used()
    {
        await using var dbContext = CreateDbContext();
        var workspaceId = await SeedGenerationContextAsync(dbContext);
        var service = CreateIdeaService(
            dbContext, workspaceId, new StubAiProvider(ValidIdeaResponse));

        await service.GenerateAsync(new GenerateContentIdeasRequest
        {
            Platform = ContentPlatform.LinkedIn,
            Count = 1
        });

        Assert.Equal(
            ContentIdeaSystemPrompt.Version,
            Assert.Single(dbContext.AiRuns).PromptVersion);
    }

    [Fact]
    public void Every_versioned_prompt_exposes_a_non_empty_version()
    {
        // The roadmap gate "prompt versions are tracked" is only satisfied if a
        // version actually exists for each prompt that can produce a run.
        Assert.False(string.IsNullOrWhiteSpace(ContentIdeaSystemPrompt.Version));
        Assert.False(string.IsNullOrWhiteSpace(ContentPostSystemPrompt.Version));
        Assert.False(string.IsNullOrWhiteSpace(ContentEvaluationSystemPrompt.Version));
    }

    [Fact]
    public async Task A_succeeded_run_records_a_duration_and_completion_timestamps()
    {
        await using var dbContext = CreateDbContext();
        var workspaceId = await SeedGenerationContextAsync(dbContext);
        var service = CreateIdeaService(
            dbContext, workspaceId, new StubAiProvider(ValidIdeaResponse));

        var before = DateTime.UtcNow;
        await service.GenerateAsync(new GenerateContentIdeasRequest
        {
            Platform = ContentPlatform.LinkedIn,
            Count = 1
        });
        var after = DateTime.UtcNow;

        var run = Assert.Single(dbContext.AiRuns);
        Assert.NotNull(run.DurationMs);
        Assert.True(run.DurationMs >= 0);
        Assert.NotNull(run.CompletedAt);
        Assert.InRange(run.CreatedAt, before.AddSeconds(-1), after.AddSeconds(1));
    }

    [Fact]
    public async Task A_succeeded_run_records_no_error_message()
    {
        await using var dbContext = CreateDbContext();
        var workspaceId = await SeedGenerationContextAsync(dbContext);
        var service = CreateIdeaService(
            dbContext, workspaceId, new StubAiProvider(ValidIdeaResponse));

        await service.GenerateAsync(new GenerateContentIdeasRequest
        {
            Platform = ContentPlatform.LinkedIn,
            Count = 1
        });

        Assert.Null(Assert.Single(dbContext.AiRuns).ErrorMessage);
    }

    [Fact]
    public async Task Token_and_cost_columns_stay_null_because_the_provider_reports_no_usage()
    {
        await using var dbContext = CreateDbContext();
        var workspaceId = await SeedGenerationContextAsync(dbContext);
        var service = CreateIdeaService(
            dbContext, workspaceId, new StubAiProvider(ValidIdeaResponse));

        await service.GenerateAsync(new GenerateContentIdeasRequest
        {
            Platform = ContentPlatform.LinkedIn,
            Count = 1
        });

        // IAiProvider returns only the generated text. An estimate written here
        // would be indistinguishable from a measurement when read back.
        var run = Assert.Single(dbContext.AiRuns);
        Assert.Null(run.InputTokens);
        Assert.Null(run.OutputTokens);
        Assert.Null(run.TotalTokens);
        Assert.Null(run.EstimatedCost);
    }

    [Fact]
    public async Task Run_metadata_is_persisted_as_json()
    {
        await using var dbContext = CreateDbContext();
        var workspaceId = await SeedGenerationContextAsync(dbContext);
        var service = CreateIdeaService(
            dbContext, workspaceId, new StubAiProvider(ValidIdeaResponse));

        await service.GenerateAsync(new GenerateContentIdeasRequest
        {
            Platform = ContentPlatform.X,
            Count = 1
        });

        var run = Assert.Single(dbContext.AiRuns);
        Assert.NotNull(run.Metadata);

        using var metadata = JsonDocument.Parse(run.Metadata);
        Assert.Equal("X", metadata.RootElement.GetProperty("platform").GetString());
        Assert.Equal(1, metadata.RootElement.GetProperty("requestedCount").GetInt32());
    }

    [Fact]
    public async Task A_failed_run_is_recorded_as_failed()
    {
        await using var dbContext = CreateDbContext();
        var workspaceId = await SeedGenerationContextAsync(dbContext);
        var service = CreateIdeaService(
            dbContext,
            workspaceId,
            new ThrowingAiProvider(new InvalidAiResponseException("bad")));

        await Assert.ThrowsAsync<InvalidAiResponseException>(() =>
            service.GenerateAsync(new GenerateContentIdeasRequest
            {
                Platform = ContentPlatform.LinkedIn,
                Count = 1
            }));

        var run = Assert.Single(dbContext.AiRuns);
        Assert.Equal(AIRunType.IdeaGeneration, run.RunType);
        Assert.Equal(AIRunStatus.Failed, run.Status);
        Assert.Equal(nameof(InvalidAiResponseException), run.ErrorMessage);
        Assert.NotNull(run.DurationMs);
        Assert.NotNull(run.CompletedAt);
    }

    [Fact]
    public async Task A_failed_run_records_the_exception_type_rather_than_its_message()
    {
        // A provider exception can quote the prompt or the raw response back.
        // That content must not be copied into a durable table.
        await using var dbContext = CreateDbContext();
        var workspaceId = await SeedGenerationContextAsync(dbContext);
        var service = CreateIdeaService(
            dbContext,
            workspaceId,
            new ThrowingAiProvider(
                new InvalidAiResponseException("SECRET_PROMPT_FRAGMENT leaked")));

        await Assert.ThrowsAsync<InvalidAiResponseException>(() =>
            service.GenerateAsync(new GenerateContentIdeasRequest
            {
                Platform = ContentPlatform.LinkedIn,
                Count = 1
            }));

        var errorMessage = Assert.Single(dbContext.AiRuns).ErrorMessage;
        Assert.Equal(nameof(InvalidAiResponseException), errorMessage);
        Assert.DoesNotContain("SECRET_PROMPT_FRAGMENT", errorMessage);
    }

    [Fact]
    public async Task A_cancelled_run_is_not_recorded_as_a_failure()
    {
        await using var dbContext = CreateDbContext();
        var workspaceId = await SeedGenerationContextAsync(dbContext);
        var service = CreateIdeaService(
            dbContext,
            workspaceId,
            new ThrowingAiProvider(new OperationCanceledException()));

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            service.GenerateAsync(
                new GenerateContentIdeasRequest
                {
                    Platform = ContentPlatform.LinkedIn,
                    Count = 1
                },
                CancellationToken.None));

        // Cancellation is not a provider failure, so it is left out rather than
        // recorded as one.
        Assert.Empty(dbContext.AiRuns);
    }

    [Fact]
    public async Task Recording_a_run_does_not_change_the_ideas_that_are_returned()
    {
        await using var dbContext = CreateDbContext();
        var workspaceId = await SeedGenerationContextAsync(dbContext);
        var service = CreateIdeaService(
            dbContext, workspaceId, new StubAiProvider(ValidIdeaResponse));

        var generated = await service.GenerateAsync(new GenerateContentIdeasRequest
        {
            Platform = ContentPlatform.LinkedIn,
            Count = 1,
            TopicHint = "Testing"
        });

        // The same values the caller saw before AI run tracking existed.
        var idea = Assert.Single(generated);
        Assert.Equal("A useful idea", idea.Title);
        Assert.Equal("Engineering", idea.Topic);
        Assert.Equal(76.50m, idea.OverallScore);
        Assert.Equal("#engineering", idea.SuggestedHashtags);
        Assert.Equal(ContentIdeaStatus.Suggested, Enum.Parse<ContentIdeaStatus>(idea.Status));
        Assert.Single(dbContext.AiRuns);
        Assert.Equal(1, dbContext.ContentIdeas.Count());
    }

    [Fact]
    public async Task A_provider_failure_still_rethrows_the_original_exception()
    {
        await using var dbContext = CreateDbContext();
        var workspaceId = await SeedGenerationContextAsync(dbContext);
        var service = CreateIdeaService(
            dbContext,
            workspaceId,
            new ThrowingAiProvider(new InvalidAiResponseException("original")));

        var thrown = await Assert.ThrowsAsync<InvalidAiResponseException>(() =>
            service.GenerateAsync(new GenerateContentIdeasRequest
            {
                Platform = ContentPlatform.LinkedIn,
                Count = 1
            }));

        // Recording must not replace the real error with a tracking error.
        Assert.Equal("original", thrown.Message);
    }

    [Fact]
    public async Task Generating_a_post_records_a_content_generation_run()
    {
        await using var dbContext = CreateDbContext();
        var workspaceId = await SeedGenerationContextAsync(dbContext);
        var service = CreatePostService(
            dbContext, workspaceId, new StubAiProvider(ValidPostResponse));

        var post = await service.GenerateFromIdeaAsync(
            SeedIdea(dbContext, workspaceId).Id,
            new GenerateContentPostRequest { Platform = ContentPlatform.LinkedIn },
            CancellationToken.None);

        Assert.NotNull(post);
        var run = Assert.Single(dbContext.AiRuns);
        Assert.Equal(AIRunType.ContentGeneration, run.RunType);
        Assert.Equal(AIRunStatus.Succeeded, run.Status);
        Assert.Equal(ContentPostSystemPrompt.Version, run.PromptVersion);
    }

    [Fact]
    public async Task A_content_generation_run_records_the_idea_and_platform_it_served()
    {
        await using var dbContext = CreateDbContext();
        var workspaceId = await SeedGenerationContextAsync(dbContext);
        var service = CreatePostService(
            dbContext, workspaceId, new StubAiProvider(ValidPostResponse));
        var idea = SeedIdea(dbContext, workspaceId);

        await service.GenerateFromIdeaAsync(
            idea.Id,
            new GenerateContentPostRequest { Platform = ContentPlatform.LinkedIn },
            CancellationToken.None);

        using var metadata = JsonDocument.Parse(Assert.Single(dbContext.AiRuns).Metadata!);
        Assert.Equal(idea.Id, metadata.RootElement.GetProperty("contentIdeaId").GetGuid());
        Assert.Equal("LinkedIn", metadata.RootElement.GetProperty("platform").GetString());
        Assert.Equal("generation", metadata.RootElement.GetProperty("operation").GetString());
    }

    [Fact]
    public async Task A_failed_post_generation_records_a_failed_content_generation_run()
    {
        await using var dbContext = CreateDbContext();
        var workspaceId = await SeedGenerationContextAsync(dbContext);
        var service = CreatePostService(
            dbContext,
            workspaceId,
            new ThrowingAiProvider(new InvalidAiResponseException("bad")));

        await Assert.ThrowsAsync<InvalidAiResponseException>(() =>
            service.GenerateFromIdeaAsync(
                SeedIdea(dbContext, workspaceId).Id,
                new GenerateContentPostRequest { Platform = ContentPlatform.LinkedIn },
                CancellationToken.None));

        var run = Assert.Single(dbContext.AiRuns);
        Assert.Equal(AIRunType.ContentGeneration, run.RunType);
        Assert.Equal(AIRunStatus.Failed, run.Status);
    }

    [Fact]
    public async Task Evaluating_a_post_records_a_quality_evaluation_run()
    {
        await using var dbContext = CreateDbContext();
        var workspaceId = await SeedGenerationContextAsync(dbContext);
        var service = CreatePostService(
            dbContext, workspaceId, new StubAiProvider(ValidEvaluationResponse));

        await service.EvaluateAsync(SeedPost(dbContext, workspaceId).Id, CancellationToken.None);

        var run = Assert.Single(dbContext.AiRuns);
        Assert.Equal(AIRunType.QualityEvaluation, run.RunType);
        Assert.Equal(AIRunStatus.Succeeded, run.Status);
        Assert.Equal(ContentEvaluationSystemPrompt.Version, run.PromptVersion);
    }

    [Fact]
    public async Task A_quality_evaluation_run_records_the_post_it_scored()
    {
        await using var dbContext = CreateDbContext();
        var workspaceId = await SeedGenerationContextAsync(dbContext);
        var service = CreatePostService(
            dbContext, workspaceId, new StubAiProvider(ValidEvaluationResponse));
        var post = SeedPost(dbContext, workspaceId);

        await service.EvaluateAsync(post.Id, CancellationToken.None);

        using var metadata = JsonDocument.Parse(Assert.Single(dbContext.AiRuns).Metadata!);
        Assert.Equal(post.Id, metadata.RootElement.GetProperty("contentPostId").GetGuid());
    }

    [Fact]
    public async Task A_failed_evaluation_records_a_failed_quality_evaluation_run()
    {
        await using var dbContext = CreateDbContext();
        var workspaceId = await SeedGenerationContextAsync(dbContext);
        var service = CreatePostService(
            dbContext,
            workspaceId,
            new ThrowingAiProvider(new InvalidAiResponseException("bad")));

        await Assert.ThrowsAsync<InvalidAiResponseException>(() =>
            service.EvaluateAsync(SeedPost(dbContext, workspaceId).Id, CancellationToken.None));

        var run = Assert.Single(dbContext.AiRuns);
        Assert.Equal(AIRunType.QualityEvaluation, run.RunType);
        Assert.Equal(AIRunStatus.Failed, run.Status);
    }

    [Fact]
    public async Task A_run_is_always_scoped_to_the_calling_workspace()
    {
        await using var dbContext = CreateDbContext();
        var callerWorkspaceId = await SeedGenerationContextAsync(dbContext);
        var service = CreateIdeaService(
            dbContext, callerWorkspaceId, new StubAiProvider(ValidIdeaResponse));

        await service.GenerateAsync(new GenerateContentIdeasRequest
        {
            Platform = ContentPlatform.LinkedIn,
            Count = 1
        });

        Assert.Equal(callerWorkspaceId, Assert.Single(dbContext.AiRuns).WorkspaceId);
    }

    [Fact]
    public async Task Two_workspaces_each_record_runs_against_only_their_own_workspace()
    {
        await using var dbContext = CreateDbContext();
        var firstWorkspaceId = await SeedGenerationContextAsync(dbContext);
        var secondWorkspaceId = await SeedGenerationContextAsync(dbContext);

        var first = CreateIdeaService(
            dbContext, firstWorkspaceId, new StubAiProvider(ValidIdeaResponse));
        await first.GenerateAsync(new GenerateContentIdeasRequest
        {
            Platform = ContentPlatform.LinkedIn,
            Count = 1
        });

        var second = CreateIdeaService(
            dbContext, secondWorkspaceId, new StubAiProvider(ValidIdeaResponse));
        await second.GenerateAsync(new GenerateContentIdeasRequest
        {
            Platform = ContentPlatform.X,
            Count = 1
        });

        Assert.Equal(2, dbContext.AiRuns.Count());
        Assert.Equal(1, dbContext.AiRuns.Count(run => run.WorkspaceId == firstWorkspaceId));
        Assert.Equal(1, dbContext.AiRuns.Count(run => run.WorkspaceId == secondWorkspaceId));
    }

    [Fact]
    public async Task Recording_a_run_does_not_throw_when_the_row_cannot_be_saved()
    {
        // The interface contract is that recording never throws, so this is
        // asserted against the real recorder with a database that rejects every
        // write, rather than against a stand-in that breaks the contract.
        var databaseName = Guid.NewGuid().ToString();
        await using var dbContext = CreateDbContext(databaseName);
        var workspaceId = await SeedGenerationContextAsync(dbContext);

        await using var failingContext = CreateDbContext(
            databaseName,
            new IInterceptor[] { new ThrowingSaveChangesInterceptor() });
        var recorder = CreateRunRecorder(failingContext, workspaceId);

        await recorder.RecordSuccessAsync(
            AIRunType.IdeaGeneration,
            ContentIdeaSystemPrompt.Version,
            TimeSpan.FromMilliseconds(120),
            new { platform = "LinkedIn" });

        // Detached, so the failed insert is not retried by an unrelated save.
        Assert.Empty(failingContext.ChangeTracker.Entries<AiRun>());
        Assert.Empty(dbContext.AiRuns);
    }

    [Fact]
    public async Task A_run_that_cannot_be_written_does_not_fail_idea_generation()
    {
        // Recording is diagnostic. If the row cannot be written the work must
        // still succeed, because a working feature must not break because its
        // telemetry could not be saved.
        var databaseName = Guid.NewGuid().ToString();
        await using var dbContext = CreateDbContext(databaseName);
        var workspaceId = await SeedGenerationContextAsync(dbContext);

        // A second context over the same database whose writes always fail,
        // which is what an unreachable database looks like to the recorder.
        var recordingContext = CreateDbContext(
            databaseName,
            new IInterceptor[] { new ThrowingSaveChangesInterceptor() });
        var service = CreateIdeaService(
            dbContext,
            workspaceId,
            new StubAiProvider(ValidIdeaResponse),
            CreateRunRecorder(recordingContext, workspaceId));

        var generated = await service.GenerateAsync(new GenerateContentIdeasRequest
        {
            Platform = ContentPlatform.LinkedIn,
            Count = 1
        });

        Assert.Single(generated);
        Assert.Equal(1, dbContext.ContentIdeas.Count());
        Assert.Empty(dbContext.AiRuns);
    }

    [Fact]
    public void Run_types_round_trip_through_their_stored_strings()
    {
        // The schema writes these SCREAMING_SNAKE_CASE. Deriving the string by
        // uppercasing the member name would yield IDEAGENERATION instead.
        Assert.Equal("IDEA_GENERATION", AIRunTypeStorage.ToStorage(AIRunType.IdeaGeneration));
        Assert.Equal("CONTENT_GENERATION", AIRunTypeStorage.ToStorage(AIRunType.ContentGeneration));
        Assert.Equal("QUALITY_EVALUATION", AIRunTypeStorage.ToStorage(AIRunType.QualityEvaluation));
        Assert.Equal("OPPORTUNITY_SCORING", AIRunTypeStorage.ToStorage(AIRunType.OpportunityScoring));

        Assert.Equal(AIRunType.IdeaGeneration, AIRunTypeStorage.FromStorage("IDEA_GENERATION"));
        Assert.Equal(AIRunType.QualityEvaluation, AIRunTypeStorage.FromStorage("QUALITY_EVALUATION"));
    }

    [Fact]
    public void Every_documented_run_type_has_a_stored_string()
    {
        foreach (var runType in Enum.GetValues<AIRunType>())
        {
            var stored = AIRunTypeStorage.ToStorage(runType);
            Assert.False(string.IsNullOrWhiteSpace(stored));
            Assert.Equal(runType, AIRunTypeStorage.FromStorage(stored));
        }
    }

    [Fact]
    public async Task A_succeeded_run_is_counted_as_a_succeeded_ai_operation()
    {
        await using var dbContext = CreateDbContext();
        var workspaceId = await SeedGenerationContextAsync(dbContext);
        var service = CreateIdeaService(
            dbContext,
            workspaceId,
            new StubAiProvider(ValidIdeaResponse));

        var before = AiOperationCount("succeeded");
        var durationBefore = AiDurationCount();

        await service.GenerateAsync(new GenerateContentIdeasRequest
        {
            Platform = ContentPlatform.LinkedIn,
            Count = 1
        });

        Assert.Equal(before + 1, AiOperationCount("succeeded"));
        Assert.Equal(durationBefore + 1, AiDurationCount());
    }

    [Fact]
    public async Task A_failed_run_is_counted_as_a_failed_ai_operation()
    {
        // An operation that failed is the one worth counting. A metric that only
        // counted successes would make an outage look like a quiet day.
        await using var dbContext = CreateDbContext();
        var workspaceId = await SeedGenerationContextAsync(dbContext);
        var service = CreateIdeaService(
            dbContext,
            workspaceId,
            new ThrowingAiProvider(new InvalidAiResponseException("bad")));

        var before = AiOperationCount("failed");

        await Assert.ThrowsAsync<InvalidAiResponseException>(() =>
            service.GenerateAsync(new GenerateContentIdeasRequest
            {
                Platform = ContentPlatform.LinkedIn,
                Count = 1
            }));

        Assert.Equal(before + 1, AiOperationCount("failed"));
    }

    [Fact]
    public async Task A_run_that_cannot_be_written_is_still_counted()
    {
        // The metric is recorded before the row is written. Telemetry that
        // depended on the database would go missing exactly when the database is
        // the thing that is broken.
        var databaseName = Guid.NewGuid().ToString();
        await using var dbContext = CreateDbContext(databaseName);
        var workspaceId = await SeedGenerationContextAsync(dbContext);

        var recordingContext = CreateDbContext(
            databaseName,
            new IInterceptor[] { new ThrowingSaveChangesInterceptor() });
        var service = CreateIdeaService(
            dbContext,
            workspaceId,
            new StubAiProvider(ValidIdeaResponse),
            CreateRunRecorder(recordingContext, workspaceId));

        var before = AiOperationCount("succeeded");

        await service.GenerateAsync(new GenerateContentIdeasRequest
        {
            Platform = ContentPlatform.LinkedIn,
            Count = 1
        });

        Assert.Equal(before + 1, AiOperationCount("succeeded"));
    }

    [Fact]
    public async Task An_ai_metric_never_carries_the_prompt_or_the_response()
    {
        // The provider response is generated content. It has no place in a metric
        // label, and there is no label here that could hold it.
        await using var dbContext = CreateDbContext();
        var workspaceId = await SeedGenerationContextAsync(dbContext);
        var service = CreateIdeaService(
            dbContext,
            workspaceId,
            new StubAiProvider(ValidIdeaResponse));

        await service.GenerateAsync(new GenerateContentIdeasRequest
        {
            Platform = ContentPlatform.LinkedIn,
            Count = 1
        });

        var series = MetricsScrape.Series("mayorx_ai_operations_total");

        Assert.All(series, sample => Assert.Equal(
            ["outcome", "provider", "run_type"],
            sample.Labels.Select(label => label.Key).OrderBy(key => key, StringComparer.Ordinal)));
        Assert.All(series, sample => Assert.All(
            sample.Labels,
            label => Assert.DoesNotContain("A useful idea", label.Value, StringComparison.Ordinal)));
    }

    /// <summary>
    /// Deltas rather than exact values: the instruments are process-wide, and this
    /// class shares its provider label with every other AI test in the run.
    /// </summary>
    private static long AiOperationCount(string outcome) =>
        MetricsScrape.CounterOrZero(
            "mayorx_ai_operations_total",
            ("provider", TestAiOptions().Provider),
            ("run_type", "idea_generation"),
            ("outcome", outcome));

    private static long AiDurationCount() =>
        MetricsScrape.HistogramCountOrZero(
            "mayorx_ai_operation_duration_seconds",
            ("provider", TestAiOptions().Provider),
            ("run_type", "idea_generation"));

    [Fact]
    public void Run_statuses_round_trip_through_their_stored_strings()
    {
        Assert.Equal("SUCCEEDED", AIRunStatusStorage.ToStorage(AIRunStatus.Succeeded));
        Assert.Equal("FAILED", AIRunStatusStorage.ToStorage(AIRunStatus.Failed));
        Assert.Equal(AIRunStatus.Succeeded, AIRunStatusStorage.FromStorage("SUCCEEDED"));
        Assert.Equal(AIRunStatus.Failed, AIRunStatusStorage.FromStorage("FAILED"));
    }

    private static AppDbContext CreateDbContext(
        string? databaseName = null,
        IEnumerable<IInterceptor>? interceptors = null)
    {
        var builder = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName ?? Guid.NewGuid().ToString());

        if (interceptors is not null)
        {
            builder.AddInterceptors(interceptors);
        }

        return new AppDbContext(builder.Options);
    }

    private static AiRunRecorder CreateRunRecorder(AppDbContext dbContext, Guid workspaceId) =>
        new(
            dbContext,
            new CurrentUserService(AuditLogTestContext.CreateAccessor(workspaceId)),
            Options.Create(TestAiOptions()),
            NullLogger<AiRunRecorder>.Instance);

    private static AiOptions TestAiOptions() => new()
    {
        Provider = "Gemini",
        Model = "test-model",
        ApiKey = "test-placeholder"
    };

    private static ContentIdeaService CreateIdeaService(
        AppDbContext dbContext,
        Guid workspaceId,
        IAiProvider aiProvider,
        IAiRunRecorder? runRecorder = null) =>
        new(
            dbContext,
            new CurrentUserService(AuditLogTestContext.CreateAccessor(workspaceId)),
            AuditLogTestContext.Create(dbContext, workspaceId),
            aiProvider,
            runRecorder ?? AiRunTestContext.Create(dbContext, workspaceId),
            new ContentIdeaPromptBuilder());

    private static ContentPostService CreatePostService(
        AppDbContext dbContext,
        Guid workspaceId,
        IAiProvider aiProvider) =>
        new(
            dbContext,
            new CurrentUserService(AuditLogTestContext.CreateAccessor(workspaceId)),
            AuditLogTestContext.Create(dbContext, workspaceId),
            aiProvider,
            AiRunTestContext.Create(dbContext, workspaceId),
            new ContentPostPromptBuilder(),
            new ContentEvaluationPromptBuilder(),
new ContentRepetitionDetector(),
            new NotSchedulingScheduledPostStore(),
            NullLogger<ContentPostService>.Instance,
            Options.Create(TestAiOptions()),
            Options.Create(new ContentQualityOptions()),
            Options.Create(new SchedulingOptions()));

    private static async Task<Guid> SeedGenerationContextAsync(AppDbContext dbContext)
    {
        var workspaceId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        dbContext.Workspaces.Add(new Workspace
        {
            Id = workspaceId,
            Name = "Workspace",
            Status = WorkspaceStatus.Active,
            CreatedAt = now,
            UpdatedAt = now
        });
        dbContext.BrandProfiles.Add(new BrandProfile
        {
            Id = Guid.NewGuid(),
            WorkspaceId = workspaceId,
            Name = "Example brand",
            CreatedAt = now,
            UpdatedAt = now
        });
        dbContext.AudienceProfiles.Add(new AudienceProfile
        {
            Id = Guid.NewGuid(),
            WorkspaceId = workspaceId,
            Description = "Software developers",
            CreatedAt = now,
            UpdatedAt = now
        });
        dbContext.ContentStrategies.Add(new ContentStrategy
        {
            Id = Guid.NewGuid(),
            WorkspaceId = workspaceId,
            ContentObjectives = "Practical engineering lessons",
            CreatedAt = now,
            UpdatedAt = now
        });

        await dbContext.SaveChangesAsync();
        return workspaceId;
    }

    private static ContentIdea SeedIdea(AppDbContext dbContext, Guid workspaceId)
    {
        var idea = new ContentIdea
        {
            Id = Guid.NewGuid(),
            WorkspaceId = workspaceId,
            Status = ContentIdeaStatus.Approved,
            Platform = ContentPlatform.LinkedIn,
            Title = "An approved idea",
            Topic = "Engineering",
            CreatedAt = DateTime.UtcNow
        };
        dbContext.ContentIdeas.Add(idea);
        dbContext.SaveChanges();
        return idea;
    }

    private static ContentPost SeedPost(AppDbContext dbContext, Guid workspaceId)
    {
        // Evaluation only runs for a post that traces back to an idea.
        var post = new ContentPost
        {
            Id = Guid.NewGuid(),
            WorkspaceId = workspaceId,
            ContentIdeaId = SeedIdea(dbContext, workspaceId).Id,
            Status = ContentPostStatus.Draft,
            Platform = ContentPlatform.LinkedIn,
            Content = "Body copy.",
            Hook = "A strong opening",
            CallToAction = "Try it",
            CreatedAt = DateTime.UtcNow
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

    private sealed class ThrowingAiProvider(Exception exception) : IAiProvider
    {
        public Task<string> GenerateAsync(
            string systemPrompt,
            string userPrompt,
            AiResponseSchema responseSchema,
            CancellationToken cancellationToken = default) =>
            Task.FromException<string>(exception);
    }

    /// <summary>
    /// Makes every write fail, standing in for a database that cannot be
    /// reached at the moment the run is recorded.
    /// </summary>
    private sealed class ThrowingSaveChangesInterceptor : SaveChangesInterceptor
    {
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("database unavailable");
    }
}
