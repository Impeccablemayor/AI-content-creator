namespace Api.Contracts.ContentPost;

public sealed class ContentPostResponse
{
    public Guid Id { get; init; }

    public Guid ContentIdeaId { get; init; }

    public string Platform { get; init; } = string.Empty;

    public string Status { get; init; } = string.Empty;

    public string Content { get; init; } = string.Empty;

    public string Hook { get; init; } = string.Empty;

    public string CallToAction { get; init; } = string.Empty;

    public IReadOnlyList<string> Hashtags { get; init; } = [];

    public string ContentType { get; init; } = string.Empty;

    public string StorytellingStructure { get; init; } = string.Empty;

    public DateTime? ScheduledAt { get; init; }

    /// <summary>When the post was successfully published. Null until then.</summary>
    public DateTime? PublishedAt { get; init; }

    /// <summary>
    /// Why the last publish attempt failed, or null when the post has not
    /// failed. The post stays Scheduled after a failure, so this is the only
    /// signal that explains a post that is overdue but not published.
    /// </summary>
    public string? PublishFailureReason { get; init; }

    public DateTime CreatedAt { get; init; }

    public DateTime UpdatedAt { get; init; }
}