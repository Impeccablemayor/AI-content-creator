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

    public DateTime CreatedAt { get; init; }

    public DateTime UpdatedAt { get; init; }
}