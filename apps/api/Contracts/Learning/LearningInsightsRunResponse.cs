namespace Api.Contracts.Learning;

/// <summary>
/// Result of one learning-loop run over a workspace's measured posts.
/// </summary>
/// <remarks>
/// A run that found nothing is still a successful run. <see cref="HasSufficientData"/>
/// being false with an empty <see cref="Insights"/> list is the honest answer for a
/// workspace that has not published or measured enough yet, and it is not an error.
/// </remarks>
public sealed class LearningInsightsRunResponse
{
    public DateTime From { get; init; }

    public DateTime To { get; init; }

    /// <summary>Published posts inside the window, whether or not they have metrics.</summary>
    public int PostsInWindow { get; init; }

    /// <summary>Published posts inside the window that have a metrics snapshot.</summary>
    public int MeasuredPosts { get; init; }

    /// <summary>Measured posts the run required before comparing anything.</summary>
    public int MinimumMeasuredPosts { get; init; }

    /// <summary>
    /// False when there was not enough measured data to compare. When false, no
    /// AI call was made and no signal was written.
    /// </summary>
    public bool HasSufficientData { get; init; }

    public IReadOnlyList<LearningInsightResponse> Insights { get; init; } = [];
}
