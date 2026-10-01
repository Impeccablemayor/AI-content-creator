namespace Api.Configuration;

/// <summary>
/// Thresholds that decide when there is enough measured performance to say
/// anything about it.
/// </summary>
/// <remarks>
/// Every value here exists to make the learning loop say less. A workspace with
/// two published posts has no pattern, only two data points, and a comparison
/// drawn from it would read as a finding while being noise. These settings are
/// the line between "report what was measured" and "compare what was measured".
/// </remarks>
public sealed class LearningLoopOptions
{
    public const string SectionName = "LearningLoop";

    /// <summary>
    /// How far back published posts are analysed. Reach accumulates after a post
    /// goes out, so a post only becomes worth comparing once it has had time to
    /// collect some.
    /// </summary>
    public int LookbackDays { get; set; } = 90;

    /// <summary>
    /// Measured posts needed in the window before any comparison is attempted.
    /// Below this the run reports insufficient data and writes nothing.
    /// </summary>
    public int MinimumMeasuredPosts { get; set; } = 5;

    /// <summary>
    /// Posts a single group needs before it is compared against another group.
    /// A group of one cannot establish a pattern, however far it sits from the
    /// rest.
    /// </summary>
    public int MinimumPostsPerGroup { get; set; } = 2;

    /// <summary>
    /// How much better the strongest group must be than the weakest before the
    /// difference is reported, as a fraction. 0.25 means the strongest group's
    /// engagement rate must be at least 25% above the weakest.
    /// </summary>
    /// <remarks>
    /// A ratio against a weakest group of exactly zero is undefined, so in that
    /// case any positive rate in the strongest group clears the test. The
    /// observation always carries the raw rates, so a reader can see the
    /// absolute size rather than only the ratio.
    /// </remarks>
    public decimal MinimumEngagementRateGap { get; set; } = 0.25m;
}
