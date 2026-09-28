namespace Api.Contracts.ContentStrategy;

public sealed class ContentStrategyResponse
{
    public Guid Id { get; init; }

    public Guid WorkspaceId { get; init; }

    public int PostsPerWeek { get; init; }

    public string ContentPillars { get; init; } = string.Empty;

    public string ContentObjectives { get; init; } = string.Empty;

    public string PreferredFormats { get; init; } = string.Empty;

    public string StorytellingStyle { get; init; } = string.Empty;

    public string HashtagStrategy { get; init; } = string.Empty;

    public string TopicsToPrioritize { get; init; } = string.Empty;

    public string TopicsToAvoid { get; init; } = string.Empty;

    public string PreferredPostingTimes { get; init; } = string.Empty;
}