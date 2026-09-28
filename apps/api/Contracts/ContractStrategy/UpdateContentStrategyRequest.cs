namespace Api.Contracts.ContentStrategy;

public sealed class UpdateContentStrategyRequest
{
    public int PostsPerWeek { get; set; }

    public string ContentPillars { get; set; } = string.Empty;

    public string ContentObjectives { get; set; } = string.Empty;

    public string PreferredFormats { get; set; } = string.Empty;

    public string StorytellingStyle { get; set; } = string.Empty;

    public string HashtagStrategy { get; set; } = string.Empty;

    public string TopicsToPrioritize { get; set; } = string.Empty;

    public string TopicsToAvoid { get; set; } = string.Empty;

    public string PreferredPostingTimes { get; set; } = string.Empty;
}