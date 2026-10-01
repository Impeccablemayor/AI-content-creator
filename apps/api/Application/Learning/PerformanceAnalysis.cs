using Api.Configuration;
using Api.Domain.Enums;

namespace Api.Application.Learning;

/// <summary>
/// One published post with the metrics that were actually measured for it.
/// </summary>
/// <remarks>
/// Only posts with a snapshot reach the analyzer. A published post nobody has
/// read metrics for contributes no evidence, and a snapshot whose values are all
/// null is a real measurement of "the platform reported nothing" — so it counts
/// as measured and the resulting group rate stays null, which makes the group
/// ineligible for comparison rather than being quietly treated as zero.
/// </remarks>
public sealed record AnalyzedPost(
    ContentPlatform Platform,
    ContentType ContentType,
    Guid? ContentPillarId,
    string? ContentPillarName,
    DateTime? PublishedAt,
    long? Impressions,
    long? Likes,
    long? Comments,
    long? Shares);

/// <summary>
/// The ways a workspace's measured posts can be compared against each other.
/// </summary>
public enum LearningDimension
{
    Platform = 1,
    ContentType = 2,
    ContentPillar = 3,
    PostingDay = 4
}

/// <summary>
/// Posts that share a value of one dimension, with the totals behind them.
/// </summary>
/// <param name="Key">Stable identifier used for ordering.</param>
/// <param name="Label">Human-readable value named in the observation.</param>
/// <param name="Posts">Measured posts in this group.</param>
/// <param name="Impressions">Total impressions across the group, or null when
/// no post in it reported impressions.</param>
/// <param name="EngagementRate">Group engagement rate, or null when impressions
/// are unknown or zero.</param>
public sealed record LearningGroup(
    string Key,
    string Label,
    int Posts,
    long? Impressions,
    decimal? EngagementRate);

/// <summary>
/// One evidence-backed difference between the strongest and weakest group of a
/// dimension.
/// </summary>
/// <param name="Observation">What was measured, written from the real totals.
/// Never a cause and never a claim about anything unmeasured.</param>
public sealed record PerformanceFinding(
    LearningDimension Dimension,
    string SignalType,
    string Observation,
    LearningGroup Best,
    LearningGroup Worst,
    IReadOnlyList<LearningGroup> Groups,
    int EvidenceCount,
    decimal Confidence);

/// <summary>
/// The result of comparing one workspace's measured posts.
/// </summary>
public sealed record PerformanceAnalysis(
    int PostsConsidered,
    int PostsMeasured,
    IReadOnlyList<PerformanceFinding> Findings)
{
    public bool HasSufficientData => Findings.Count > 0;
}
