namespace Api.Domain.Entities;

/// <summary>
/// A performance snapshot for one published content post.
/// </summary>
/// <remarks>
/// <para>
/// Every metric is nullable and that is deliberate, not laziness. The design
/// requires "metric unavailable", "metric = 0" and "metric not yet
/// synchronized" to stay three different states, and a non-nullable column
/// cannot tell them apart. A null <see cref="Impressions"/> means no snapshot
/// has reported it yet; a zero means the platform reported zero.
/// </para>
/// <para>
/// A post may have several snapshots because platform statistics keep moving.
/// Reads take the most recent one per post; see
/// <c>PublishedPostMetricsReader</c>.
/// </para>
/// <para>
/// Snapshots are written by the analytics sync worker, which reads them back from
/// the platform, and by nothing else. Both the analytics endpoints and the
/// learning loop read this table through the one reader above, so they cannot
/// disagree about what the current metrics of a post are.
/// </para>
/// </remarks>
public sealed class ContentPostMetric
{
    public Guid Id { get; set; }

    public Guid WorkspaceId { get; set; }

    public Guid ContentPostId { get; set; }

    public long? Impressions { get; set; }

    public long? Likes { get; set; }

    public long? Comments { get; set; }

    public long? Shares { get; set; }

    public long? Clicks { get; set; }

    public long? ProfileVisits { get; set; }

    public long? FollowersGained { get; set; }

    /// <summary>
    /// When the snapshot was taken. Used to pick the current values for a post.
    /// </summary>
    public DateTime RecordedAt { get; set; }

    public DateTime CreatedAt { get; set; }

    public ContentPost ContentPost { get; set; } = null!;
}
