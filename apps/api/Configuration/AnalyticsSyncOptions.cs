namespace Api.Configuration;

public sealed class AnalyticsSyncOptions
{
    public const string SectionName = "AnalyticsSync";

    /// <summary>How often the worker looks for posts whose metrics are due.</summary>
    public int PollIntervalSeconds { get; set; } = 300;

    /// <summary>
    /// Posts read per poll. Kept small because LinkedIn answers one metric per
    /// request, so a post costs several calls.
    /// </summary>
    public int BatchSize { get; set; } = 10;

    /// <summary>
    /// How long before a post's metrics are read again. Platform statistics
    /// move slowly, and this also backstops a post that can never be measured.
    /// </summary>
    public int ResyncIntervalHours { get; set; } = 24;
}
