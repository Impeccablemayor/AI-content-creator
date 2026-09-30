using System.Text.Json;
using Api.Domain.Entities;
using Api.Domain.Enums;

namespace Api.Application.AI.Prompts;

public sealed class ContentPostPromptBuilder
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public string Build(
        BrandProfile brand,
        AudienceProfile audience,
        ContentStrategy strategy,
        ContentIdea idea,
        ContentPlatform platform,
        DateTimeOffset currentTime)
    {
        var platformInstructions = platform switch
        {
            ContentPlatform.LinkedIn => new
            {
                maxContentCharacters = 3000,
                hashtagLimit = "0-5 relevant hashtags",
                guidance = "Professional insight, a curiosity-building opening, short paragraphs, useful lessons, and a natural discussion CTA. Use a story structure only when the supplied idea supports it."
            },
            ContentPlatform.X => new
            {
                maxContentCharacters = 280,
                hashtagLimit = "0-3 relevant hashtags",
                guidance = "One concise post, strong opening, high information density, one clear takeaway, optional CTA. Do not produce a thread."
            },
            _ => throw new ArgumentOutOfRangeException(nameof(platform))
        };

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
            contentStrategy = new
            {
                strategy.ContentPillars,
                strategy.ContentObjectives,
                strategy.PreferredFormats,
                strategy.StorytellingStyle,
                strategy.HashtagStrategy,
                strategy.TopicsToPrioritize,
                strategy.TopicsToAvoid
            },
            contentIdea = new
            {
                idea.Title,
                idea.Topic,
                idea.Angle,
                idea.Hook,
                idea.Reason,
                idea.SuggestedHashtags
            },
            platform = platform.ToString(),
            platformInstructions,
            currentDate = currentTime.ToString("O")
        };

        return JsonSerializer.Serialize(context, JsonOptions);
    }
}