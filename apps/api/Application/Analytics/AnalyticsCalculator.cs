namespace Api.Application.Analytics;

/// <summary>
/// Metric arithmetic, kept free of EF and HTTP so the rules are testable on
/// their own and cannot drift from the documented definitions.
/// </summary>
public static class AnalyticsCalculator
{
    /// <summary>
    /// Engagement rate as a percentage of impressions.
    /// </summary>
    /// <remarks>
    /// The formula is (likes + comments + shares) / impressions. Clicks,
    /// profile visits and followers are excluded: they describe reach and
    /// audience growth rather than engagement with the post itself. Summing
    /// first and dividing once is deliberate, because averaging per-post rates
    /// would weight a post with 3 impressions the same as one with 30,000.
    /// </remarks>
    /// <returns>
    /// Null when impressions are unknown or zero, since no rate exists then.
    /// </returns>
    public static decimal? EngagementRate(long? likes, long? comments, long? shares, long? impressions)
    {
        if (impressions is null or 0)
        {
            return null;
        }

        var engaged = (likes ?? 0) + (comments ?? 0) + (shares ?? 0);
        return Math.Round(engaged * 100m / impressions.Value, 2, MidpointRounding.AwayFromZero);
    }

    /// <summary>
    /// Sums one metric across snapshots.
    /// </summary>
    /// <remarks>
    /// Null is treated as "this snapshot did not report the metric" and is
    /// skipped. If no snapshot reported it at all the result stays null, which
    /// is how a total of "unknown" is distinguished from a total of zero.
    /// </remarks>
    public static long? Sum(IEnumerable<long?> values)
    {
        long total = 0;
        var reported = false;

        foreach (var value in values)
        {
            if (value is null)
            {
                continue;
            }

            total += value.Value;
            reported = true;
        }

        return reported ? total : null;
    }
}
