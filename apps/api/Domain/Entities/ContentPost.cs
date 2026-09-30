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

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public ContentIdea ContentIdea { get; set; } = null!;
}