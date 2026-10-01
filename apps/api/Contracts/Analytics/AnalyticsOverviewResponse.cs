namespace Api.Contracts.Analytics;

/// <summary>
/// Workspace-level rollup over published posts.
/// </summary>
/// <remarks>
/// Every metric is nullable. A null means no published post in the period has
/// reported that metric, which is a different claim from "the total is zero".
/// This is the "metric unavailable" versus "metric = 0" distinction the design
/// requires, preserved end to end.
/// </remarks>
public sealed class AnalyticsOverviewResponse
{
    /// <summary>
    /// The period actually applied. Bounds are null when the caller did not
    /// filter, which means all time rather than a default window.
    /// </summary>
    public AnalyticsPeriodResponse Period { get; init; } = new();

    public int PostsPublished { get; init; }

    public long? Impressions { get; init; }

    public long? Likes { get; init; }

    public long? Comments { get; init; }

    public long? Shares { get; init; }

    public long? Clicks { get; init; }

    public long? ProfileVisits { get; init; }

    public long? FollowersGained { get; init; }

    /// <summary>
    /// (likes + comments + shares) / impressions, as a percentage. Null when
    /// impressions are unknown or zero, because no rate is defined in that
    /// case. Not stored: it is always derived from the two totals so a
    /// workspace-level rate can never disagree with its own inputs.
    /// </summary>
    public decimal? EngagementRate { get; init; }
}

public sealed class AnalyticsPeriodResponse
{
    public DateTime? From { get; init; }

    public DateTime? To { get; init; }
}
