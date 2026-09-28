using System.Text.Json;
using Api.Domain.Entities;
using Api.Domain.Enums;

namespace Api.Application.AI.Prompts;

public sealed class ContentIdeaPromptBuilder
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public string Build(
        BrandProfile brand,
        AudienceProfile audience,
        ContentStrategy strategy,
        ContentPlatform platform,
        int count,
        string? topicHint,
        DateTimeOffset currentTime)
    {
        var context = new
        {
            brand = new
            {
                brand.Name,
                brand.Description,
                brand.Industry,
                brand.ToneOfVoice,
                brand.ContentGoals
            },
            audience = new
            {
                audience.Description,
                audience.Demographics,
                audience.Interests,
                audience.PainPoints,
                audience.Goals
            },
            strategy = new
            {
                strategy.ContentPillars,
                strategy.ContentObjectives,
                strategy.PreferredFormats,
                strategy.StorytellingStyle,
                strategy.HashtagStrategy,
                strategy.TopicsToPrioritize,
                strategy.TopicsToAvoid,
                strategy.PreferredPostingTimes
            },
            requestedPlatform = platform.ToString(),
            platformGuidance = platform switch
            {
                ContentPlatform.LinkedIn =>
                    "Professional insight, career and technical lessons, educational storytelling, industry observations, useful frameworks, substantive discussion.",
                ContentPlatform.X =>
                    "Concise insights, strong hooks, opinions, observations, short thread concepts, conversational writing, technical insights.",
                _ => string.Empty
            },
            hashtagGuidance = platform switch
            {
                ContentPlatform.LinkedIn =>
                    "Prefer 0-3 highly relevant hashtags; follow the workspace hashtag strategy.",
                ContentPlatform.X =>
                    "Prefer 0-1 highly relevant hashtag; follow the workspace hashtag strategy.",
                _ => string.Empty
            },
            requestedCount = count,
            topicHint,
            currentTime = currentTime.ToString("O")
        };

        return JsonSerializer.Serialize(context, JsonOptions);
    }
}