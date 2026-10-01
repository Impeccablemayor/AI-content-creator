namespace Api.Contracts.Learning;

/// <summary>
/// One stored learning signal.
/// </summary>
/// <remarks>
/// Observation and recommendation are separate fields because they are different
/// kinds of claim. The observation is what was measured and can be recomputed
/// from the metrics at any time. The recommendation is the model's reading of
/// that evidence, kept apart so a reader can accept one without the other.
/// </remarks>
public sealed class LearningInsightResponse
{
    public Guid Id { get; init; }

    /// <summary>
    /// Signal kind, such as <c>PLATFORM_ENGAGEMENT</c> or
    /// <c>CONTENT_PILLAR_ENGAGEMENT</c>.
    /// </summary>
    public string Type { get; init; } = string.Empty;

    /// <summary>The dimension the compared groups belong to, such as <c>Platform</c>.</summary>
    public string Dimension { get; init; } = string.Empty;

    /// <summary>What was measured. Stated from real totals, never inferred.</summary>
    public string Observation { get; init; } = string.Empty;

    /// <summary>What the model suggests trying next. An inference, not a result.</summary>
    public string? Recommendation { get; init; }

    /// <summary>Measured posts behind this signal.</summary>
    public int EvidenceCount { get; init; }

    /// <summary>
    /// Strength of evidence on a 0-100 scale, derived from how many posts each
    /// compared group had. Not a probability.
    /// </summary>
    public decimal? Confidence { get; init; }

    public DateTime CreatedAt { get; init; }
}
