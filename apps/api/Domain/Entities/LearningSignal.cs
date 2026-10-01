namespace Api.Domain.Entities;

/// <summary>
/// One evidence-based observation about how a workspace's published content has
/// actually performed, and what that might mean for the next piece of content.
/// </summary>
/// <remarks>
/// <para>
/// This is the entity the design calls <c>learning_signals</c>. It is the first
/// table in the learning loop: published content produces performance metrics,
/// the metrics are compared across a dimension, and the comparison is stored
/// here so it can be read back later.
/// </para>
/// <para>
/// The split inside a row is deliberate. <see cref="Signal"/> is the
/// observation, and it is written by code from the real metric totals — never by
/// the model. The interpretation is kept apart in <see cref="Metadata"/>, so a
/// reader can always see which half is a measurement and which half is
/// inference. A single merged sentence could not carry that distinction once it
/// was read back.
/// </para>
/// <para>
/// Rows accumulate. A later run over a longer history is a new observation, not
/// a correction of the old one, so nothing here is updated in place.
/// </para>
/// </remarks>
public sealed class LearningSignal
{
    public Guid Id { get; set; }

    public Guid WorkspaceId { get; set; }

    /// <summary>
    /// What produced this signal, for example <c>PerformanceAnalysis</c>.
    /// </summary>
    public string SourceType { get; set; } = string.Empty;

    /// <summary>
    /// The record the signal was derived from. Null when the evidence spans many
    /// records, which is the normal case: a platform or pillar comparison is
    /// measured over every post in the window, not over one post.
    /// </summary>
    public Guid? SourceId { get; set; }

    /// <summary>
    /// What the signal is about, for example <c>PLATFORM_ENGAGEMENT</c>.
    /// </summary>
    public string SignalType { get; set; } = string.Empty;

    /// <summary>
    /// The observation, written from the measured numbers. States what was
    /// measured and on how many posts, and claims nothing beyond that.
    /// </summary>
    public string Signal { get; set; } = string.Empty;

    /// <summary>
    /// How much measured evidence stands behind this signal, from 0 to 100.
    /// </summary>
    /// <remarks>
    /// A deterministic function of the smaller of the two compared group sizes,
    /// documented on the type that computes it. It is a strength-of-evidence
    /// indicator for ranking signals against each other. It is not a probability
    /// and not a statistical confidence interval, because no such statistic is
    /// computed here.
    /// </remarks>
    public decimal? Confidence { get; set; }

    /// <summary>
    /// The evidence behind the observation — the compared groups, their metric
    /// totals and sample sizes — plus the inferred recommendation, kept separate
    /// from the observation. A JSON object.
    /// </summary>
    public string? Metadata { get; set; }

    public DateTime CreatedAt { get; set; }

    public Workspace Workspace { get; set; } = null!;
}
