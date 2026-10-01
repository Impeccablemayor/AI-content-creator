namespace Api.Contracts.Analytics;

/// <summary>
/// Performance for one published content post.
/// </summary>
public sealed class PostAnalyticsResponse
{
    public Guid ContentPostId { get; init; }

    public string Platform { get; init; } = string.Empty;

    /// <summary>
    /// The pillar the originating idea was attributed to, when it had one.
    /// </summary>
    public Guid? ContentPillarId { get; init; }

    public string? ContentPillarName { get; init; }

    public DateTime? PublishedAt { get; init; }

    /// <summary>
    /// When the reported values were measured. Null when the post has no
    /// snapshot yet, which is how a caller tells "no data" from "zero data".
    /// </summary>
    public DateTime? RecordedAt { get; init; }

    public long? Impressions { get; init; }

    public long? Likes { get; init; }

    public long? Comments { get; init; }

    public long? Shares { get; init; }

    public long? Clicks { get; init; }

    public long? ProfileVisits { get; init; }

    public long? FollowersGained { get; init; }

    public decimal? EngagementRate { get; init; }
}
