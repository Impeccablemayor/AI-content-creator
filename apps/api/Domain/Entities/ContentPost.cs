using Api.Domain.Enums;

namespace Api.Domain.Entities;

public sealed class ContentPost
{
    public Guid Id { get; set; }

    public Guid WorkspaceId { get; set; }

    public Guid ContentIdeaId { get; set; }

    public ContentPlatform Platform { get; set; }

    public ContentPostStatus Status { get; set; } = ContentPostStatus.Draft;

    public string Content { get; set; } = string.Empty;

    public string Hook { get; set; } = string.Empty;

    public string CallToAction { get; set; } = string.Empty;

    public List<string> Hashtags { get; set; } = [];

    public ContentType ContentType { get; set; }

    public string StorytellingStructure { get; set; } = string.Empty;

    public DateTime? ScheduledAt { get; set; }

    /// <summary>
    /// Lease held by the worker currently publishing this post. Set when the
    /// post is claimed and cleared once publishing reaches a terminal result.
    /// </summary>
    public DateTime? PublishClaimedAt { get; set; }

    /// <summary>When the post was successfully published.</summary>
    public DateTime? PublishedAt { get; set; }

    /// <summary>
    /// The identifier the platform gave the published post, for example a tweet
    /// id or a LinkedIn URN. Null until a publish succeeds, and null for posts
    /// published before this column existed, which are never back-filled because
    /// the platform ids cannot be recovered after the fact.
    /// </summary>
    public string? PublishedExternalPostId { get; set; }

    /// <summary>
    /// When metrics were last requested for this post, successful or not. This is
    /// the only resync signal, so a post the platforms cannot report on stops
    /// being retried on every poll instead of failing forever.
    /// </summary>
    public DateTime? MetricsLastAttemptedAt { get; set; }

    /// <summary>
    /// Why the most recent publish attempt failed. Null when the post has
    /// never failed, or failed and has since been published.
    /// </summary>
    public PublishFailureReason? PublishFailureReason { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public ContentIdea ContentIdea { get; set; } = null!;

    public List<ContentPostMetric> Metrics { get; set; } = [];
}