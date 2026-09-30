using System.Text.Json;
using Api.Domain.Entities;
using Api.Domain.Enums;

namespace Api.Application.AI.Prompts;

public sealed class ContentEvaluationPromptBuilder
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
        ContentPost post)
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
                idea.Reason
            },
            platform = post.Platform.ToString(),
            platformLimits = post.Platform switch
            {
                ContentPlatform.LinkedIn => new { maxContentCharacters = 3000 },
                ContentPlatform.X => new { maxContentCharacters = 280 },
                _ => throw new ArgumentOutOfRangeException(nameof(post))
            },
            postToEvaluate = new
            {
                post.Content,
                post.Hook,
                post.CallToAction,
                post.Hashtags,
                contentType = post.ContentType.ToString(),
                post.StorytellingStructure
            },
            evaluationContext = new
            {
                postStatus = post.Status.ToString(),
                note = "Score only what is written in postToEvaluate. Do not assume anything about results, customers, or performance."
            }
        };

        return JsonSerializer.Serialize(context, JsonOptions);
    }
}
