using System.Diagnostics;
using System.Text.Json;
using Api.Application.AI;
using Api.Application.AI.Models;
using Api.Application.AI.Prompts;
using Api.Application.Analytics;
using Api.Application.Authentication;
using Api.Contracts.Learning;
using Api.Configuration;
using Api.Domain.Entities;
using Api.Domain.Enums;
using Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Api.Application.Learning;

public interface ILearningService
{
    /// <summary>
    /// Compares the workspace's measured posts, asks the model what each
    /// difference might mean, and stores the results as learning signals.
    /// </summary>
    Task<LearningInsightsRunResponse> GenerateInsightsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Stored learning signals, newest first.
    /// </summary>
    Task<IReadOnlyList<LearningInsightResponse>> GetInsightsAsync(
        int limit,
        CancellationToken cancellationToken = default);
}

public sealed class LearningService : ILearningService
{
    /// <summary>
    /// Upper bound on a read. Signals accumulate, and a larger page would be a
    /// dashboard rather than a recent-history list.
    /// </summary>
    public const int MaxInsightsLimit = 200;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private static readonly JsonSerializerOptions MetadataOptions = new()
    {
        WriteIndented = false
    };

    private readonly AppDbContext _dbContext;
    private readonly CurrentUserService _currentUser;
    private readonly IPublishedPostMetricsReader _reader;
    private readonly IAiProvider _aiProvider;
    private readonly IAiRunRecorder _aiRunRecorder;
    private readonly LearningInsightPromptBuilder _promptBuilder;
    private readonly LearningLoopOptions _options;
    private readonly AiOptions _aiOptions;
    private readonly ILogger<LearningService> _logger;

    public LearningService(
        AppDbContext dbContext,
        CurrentUserService currentUser,
        IPublishedPostMetricsReader reader,
        IAiProvider aiProvider,
        IAiRunRecorder aiRunRecorder,
        LearningInsightPromptBuilder promptBuilder,
        IOptions<LearningLoopOptions> options,
        IOptions<AiOptions> aiOptions,
        ILogger<LearningService> logger)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
        _reader = reader;
        _aiProvider = aiProvider;
        _aiRunRecorder = aiRunRecorder;
        _promptBuilder = promptBuilder;
        _options = options.Value;
        _aiOptions = aiOptions.Value;
        _logger = logger;
    }

    public async Task<LearningInsightsRunResponse> GenerateInsightsAsync(
        CancellationToken cancellationToken = default)
    {
        var workspaceId = _currentUser.WorkspaceId;
        var to = DateTime.UtcNow;
        var from = to.AddDays(-_options.LookbackDays);

        var measured = await _reader.ReadAsync(
            workspaceId,
            new AnalyticsQuery(From: from, To: to),
            cancellationToken);

        var analysis = PerformanceAnalyzer.Analyze(
            measured
                .Where(item => item.Latest is not null)
                .Select(ToAnalyzedPost)
                .ToList(),
            measured.Count,
            _options);

        if (!analysis.HasSufficientData)
        {
            // Reported as a short count and no signals, rather than as a finding
            // about a workspace that has not measured enough yet. No AI call is
            // made: there is nothing to ask about.
            _logger.LogInformation(
                "Learning run for workspace {WorkspaceId} produced no insights: {MeasuredPosts} measured posts of {MinimumMeasuredPosts} required, {PostsInWindow} published posts in the last {LookbackDays} days.",
                workspaceId,
                analysis.PostsMeasured,
                _options.MinimumMeasuredPosts,
                analysis.PostsConsidered,
                _options.LookbackDays);

            return new LearningInsightsRunResponse
            {
                From = from,
                To = to,
                PostsInWindow = analysis.PostsConsidered,
                MeasuredPosts = analysis.PostsMeasured,
                MinimumMeasuredPosts = _options.MinimumMeasuredPosts,
                HasSufficientData = false,
                Insights = []
            };
        }

        var context = await LoadContextAsync(workspaceId, cancellationToken);
        var now = DateTime.UtcNow;
        var signals = new List<LearningSignal>(analysis.Findings.Count);

        // Sequential on purpose. Each recommendation is independent, so order
        // would not change the results, but one call at a time keeps a run from
        // opening several provider requests at once and keeps the run rows in
        // the same order as the findings.
        foreach (var finding in analysis.Findings)
        {
            var recommendation = await GenerateRecommendationAsync(
                workspaceId,
                finding,
                from,
                to,
                analysis,
                context,
                cancellationToken);

            var signal = new LearningSignal
            {
                Id = Guid.NewGuid(),
                WorkspaceId = workspaceId,
                SourceType = SourceType,
                // Left null on purpose: a platform or pillar comparison is
                // measured over every post in the window, so pointing at a single
                // record would misstate where the evidence came from.
                SourceId = null,
                SignalType = finding.SignalType,
                Signal = finding.Observation,
                Confidence = finding.Confidence,
                Metadata = JsonSerializer.Serialize(
                    Metadata(finding, from, to, analysis, recommendation),
                    MetadataOptions),
                CreatedAt = now
            };

            signals.Add(signal);
        }

        // Tracked only after every recommendation succeeded. AiRunRecorder shares
        // this DbContext and saves on its own when a call fails, so adding each
        // signal inside the loop would let a later failure flush the earlier
        // ones and leave a partial run behind.
        _dbContext.LearningSignals.AddRange(signals);

        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Learning run for workspace {WorkspaceId} stored {SignalCount} signals from {MeasuredPosts} measured posts.",
            workspaceId,
            signals.Count,
            analysis.PostsMeasured);

        return new LearningInsightsRunResponse
        {
            From = from,
            To = to,
            PostsInWindow = analysis.PostsConsidered,
            MeasuredPosts = analysis.PostsMeasured,
            MinimumMeasuredPosts = _options.MinimumMeasuredPosts,
            HasSufficientData = true,
            Insights = signals.Select(ToResponse).ToList()
        };
    }

    public async Task<IReadOnlyList<LearningInsightResponse>> GetInsightsAsync(
        int limit,
        CancellationToken cancellationToken = default)
    {
        var capped = Math.Clamp(limit, 1, MaxInsightsLimit);

        var signals = await _dbContext.LearningSignals.AsNoTracking()
            .Where(signal => signal.WorkspaceId == _currentUser.WorkspaceId)
            // Id breaks the tie when two signals share a timestamp, so repeated
            // reads of the same page do not reorder.
            .OrderByDescending(signal => signal.CreatedAt)
            .ThenByDescending(signal => signal.Id)
            .Take(capped)
            .ToListAsync(cancellationToken);

        return signals.Select(ToResponse).ToList();
    }

    private const string SourceType = "PerformanceAnalysis";

    private async Task<string> GenerateRecommendationAsync(
        Guid workspaceId,
        PerformanceFinding finding,
        DateTime from,
        DateTime to,
        PerformanceAnalysis analysis,
        (BrandProfile? Brand, AudienceProfile? Audience, ContentStrategy? Strategy) context,
        CancellationToken cancellationToken)
    {
        var startedAt = Stopwatch.GetTimestamp();

        _logger.LogInformation(
            "Learning insight started for workspace {WorkspaceId}, dimension {Dimension}, provider {Provider}, model {Model}.",
            workspaceId,
            finding.Dimension,
            _aiOptions.Provider,
            _aiOptions.Model);

        try
        {
            var prompt = _promptBuilder.Build(
                finding,
                from,
                to,
                analysis.PostsConsidered,
                analysis.PostsMeasured,
                context.Brand,
                context.Audience,
                context.Strategy);

            var json = await _aiProvider.GenerateAsync(
                LearningInsightSystemPrompt.Value,
                prompt,
                LearningInsightResponseSchema.Value,
                cancellationToken);

            LearningInsightResult? parsed;
            try
            {
                parsed = JsonSerializer.Deserialize<LearningInsightResult>(json, JsonOptions);
            }
            catch (JsonException)
            {
                throw new LearningInsightFailedException(
                    "The AI recommendation was unusable. The response was not valid structured JSON.");
            }

            var recommendation = LearningInsightValidator.ValidateAndNormalize(parsed);

            await _aiRunRecorder.RecordSuccessAsync(
                AIRunType.PerformanceAnalysis,
                LearningInsightSystemPrompt.Version,
                Stopwatch.GetElapsedTime(startedAt),
                new
                {
                    signalType = finding.SignalType,
                    dimension = finding.Dimension.ToString(),
                    evidenceCount = finding.EvidenceCount
                },
                cancellationToken);

            return recommendation;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            await _aiRunRecorder.RecordFailureAsync(
                AIRunType.PerformanceAnalysis,
                LearningInsightSystemPrompt.Version,
                Stopwatch.GetElapsedTime(startedAt),
                exception,
                new
                {
                    signalType = finding.SignalType,
                    dimension = finding.Dimension.ToString(),
                    evidenceCount = finding.EvidenceCount
                },
                cancellationToken);

            _logger.LogError(
                exception,
                "Learning insight failed for workspace {WorkspaceId}, dimension {Dimension}, provider {Provider}, model {Model} after {DurationMilliseconds} ms with {FailureType}.",
                workspaceId,
                finding.Dimension,
                _aiOptions.Provider,
                _aiOptions.Model,
                (long)Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds,
                exception.GetType().Name);

            throw;
        }
    }

    private static AnalyzedPost ToAnalyzedPost(MeasuredPost measured) => new(
        measured.Post.Platform,
        measured.Post.ContentType,
        measured.Post.ContentIdea?.ContentPillar?.Id ?? measured.Post.ContentIdea?.ContentPillarId,
        measured.Post.ContentIdea?.ContentPillar?.Name,
        measured.Post.PublishedAt,
        measured.Latest?.Impressions,
        measured.Latest?.Likes,
        measured.Latest?.Comments,
        measured.Latest?.Shares);

    /// <summary>
    /// Loads the workspace's own context, all optional.
    /// </summary>
    /// <remarks>
    /// Unlike content generation, a missing profile does not block the run. The
    /// evidence was measured whether or not anyone filled in a brand profile, and
    /// the prompt states that an absent section means unknown rather than empty.
    /// </remarks>
    private async Task<(BrandProfile? Brand, AudienceProfile? Audience, ContentStrategy? Strategy)>
        LoadContextAsync(Guid workspaceId, CancellationToken cancellationToken)
    {
        var brand = await _dbContext.BrandProfiles.AsNoTracking()
            .SingleOrDefaultAsync(profile => profile.WorkspaceId == workspaceId, cancellationToken);
        var audience = await _dbContext.AudienceProfiles.AsNoTracking()
            .SingleOrDefaultAsync(profile => profile.WorkspaceId == workspaceId, cancellationToken);
        var strategy = await _dbContext.ContentStrategies.AsNoTracking()
            .SingleOrDefaultAsync(profile => profile.WorkspaceId == workspaceId, cancellationToken);

        return (brand, audience, strategy);
    }

    private static object Metadata(
        PerformanceFinding finding,
        DateTime from,
        DateTime to,
        PerformanceAnalysis analysis,
        string recommendation) => new
        {
            // Everything needed to recompute the observation from the metrics
            // without trusting the sentence that states it.
            dimension = finding.Dimension.ToString(),
            window = new { from, to },
            measuredPosts = analysis.PostsMeasured,
            publishedPostsInWindow = analysis.PostsConsidered,
            evidenceCount = finding.EvidenceCount,
            best = Describe(finding.Best),
            worst = Describe(finding.Worst),
            groups = finding.Groups.Select(Describe).ToList(),
            // Kept in its own field so the inference is never read as part of
            // the measurement.
            recommendation
        };

    private static object Describe(LearningGroup group) => new
    {
        key = group.Key,
        label = group.Label,
        posts = group.Posts,
        impressions = group.Impressions,
        engagementRate = group.EngagementRate
    };

    private static LearningInsightResponse ToResponse(LearningSignal signal) => new()
    {
        Id = signal.Id,
        Type = signal.SignalType,
        Dimension = ReadDimension(signal),
        Observation = signal.Signal,
        Recommendation = ReadRecommendation(signal),
        EvidenceCount = ReadEvidenceCount(signal),
        Confidence = signal.Confidence,
        CreatedAt = signal.CreatedAt
    };

    private static string ReadDimension(LearningSignal signal) =>
        Read(signal, out string value, "dimension") ? value : string.Empty;

    private static string? ReadRecommendation(LearningSignal signal) =>
        Read(signal, out string value, "recommendation") ? value : null;

    private static int ReadEvidenceCount(LearningSignal signal) =>
        Read(signal, out int value, "evidenceCount") ? value : 0;

    /// <summary>
    /// Reads one field from a signal's metadata JSON.
    /// </summary>
    /// <remarks>
    /// Reading the row through its own JSON rather than duplicating the values
    /// into columns is what keeps one signal from carrying two versions of the
    /// same number. A row whose metadata cannot be read still returns its
    /// observation and confidence: the measurement is intact even if the
    /// evidence detail around it is not, and failing the whole read over it would
    /// hide real signals.
    /// </remarks>
    private static bool Read<T>(LearningSignal signal, out T value, string propertyName)
    {
        value = default!;

        if (string.IsNullOrWhiteSpace(signal.Metadata))
        {
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(signal.Metadata);
            if (!document.RootElement.TryGetProperty(propertyName, out var property))
            {
                return false;
            }

            value = property.Deserialize<T>(JsonOptions)!;
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
