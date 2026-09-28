namespace Api.Domain.Entities;

public sealed class ContentStrategy
{
    public Guid Id { get; set; }

    public Guid WorkspaceId { get; set; }

    public int PostsPerWeek { get; set; } = 5;

    public string ContentPillars { get; set; } = string.Empty;

    public string ContentObjectives { get; set; } = string.Empty;

    public string PreferredFormats { get; set; } = string.Empty;

    public string StorytellingStyle { get; set; } = string.Empty;

    public string HashtagStrategy { get; set; } = string.Empty;

    public string TopicsToPrioritize { get; set; } = string.Empty;

    public string TopicsToAvoid { get; set; } = string.Empty;

    public string PreferredPostingTimes { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public Workspace Workspace { get; set; } = null!;
}