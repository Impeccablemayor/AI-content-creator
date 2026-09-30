using Api.Application.AI.Models;
using Api.Application.ContentIdeas;
using Api.Application.ContentPosts;
using Api.Domain.Enums;
using Xunit;

namespace Api.Tests;

public sealed class ContentPostGenerationValidatorTests
{
    [Fact]
    public void Valid_linkedin_post_is_normalized()
    {
        var result = ContentPostGenerationValidator.ValidateAndNormalize(
            CreateResult("LinkedIn", "Educational", ["AI", "#SoftwareEngineering", "#AI"]),
            ContentPlatform.LinkedIn);

        Assert.Equal("LinkedIn", result.Platform);
        Assert.Equal("Educational", result.ContentType);
        Assert.Equal(["#AI", "#SoftwareEngineering"], result.Hashtags);
        Assert.StartsWith("A useful opening", result.Content);
    }

    [Fact]
    public void Valid_x_post_within_the_character_limit_is_accepted()
    {
        var result = CreateResult("X", "Insight", []);
        result = new ContentPostGenerationResult
        {
            Platform = result.Platform,
            Content = new string('x', 280),
            Hook = result.Hook,
            CallToAction = string.Empty,
            Hashtags = result.Hashtags,
            ContentType = result.ContentType,
            StorytellingStructure = result.StorytellingStructure
        };

        var normalized = ContentPostGenerationValidator.ValidateAndNormalize(
            result,
            ContentPlatform.X);

        Assert.Equal(280, normalized.Content!.Length);
    }

    [Fact]
    public void Missing_content_is_rejected()
    {
        var result = CreateResult("LinkedIn", "Educational", []);
        result = new ContentPostGenerationResult
        {
            Platform = result.Platform,
            Content = " ",
            Hook = result.Hook,
            CallToAction = result.CallToAction,
            Hashtags = result.Hashtags,
            ContentType = result.ContentType,
            StorytellingStructure = result.StorytellingStructure
        };

        Assert.Throws<InvalidAiResponseException>(() =>
            ContentPostGenerationValidator.ValidateAndNormalize(
                result,
                ContentPlatform.LinkedIn));
    }

    [Fact]
    public void Mismatched_or_invalid_platform_is_rejected()
    {
        var result = CreateResult("TikTok", "Educational", []);

        Assert.Throws<InvalidAiResponseException>(() =>
            ContentPostGenerationValidator.ValidateAndNormalize(
                result,
                ContentPlatform.LinkedIn));
    }

    [Fact]
    public void Unknown_content_type_is_rejected()
    {
        var result = CreateResult("LinkedIn", "Motivational", []);

        Assert.Throws<InvalidAiResponseException>(() =>
            ContentPostGenerationValidator.ValidateAndNormalize(
                result,
                ContentPlatform.LinkedIn));
    }

    [Fact]
    public void Excessive_x_content_length_is_rejected()
    {
        var result = CreateResult("X", "Insight", []);
        result = new ContentPostGenerationResult
        {
            Platform = result.Platform,
            Content = new string('x', 281),
            Hook = result.Hook,
            CallToAction = result.CallToAction,
            Hashtags = result.Hashtags,
            ContentType = result.ContentType,
            StorytellingStructure = result.StorytellingStructure
        };

        Assert.Throws<InvalidAiResponseException>(() =>
            ContentPostGenerationValidator.ValidateAndNormalize(
                result,
                ContentPlatform.X));
    }

    [Fact]
    public void Invalid_hashtag_syntax_is_rejected()
    {
        var result = CreateResult("LinkedIn", "Educational", ["#two words"]);

        Assert.Throws<InvalidAiResponseException>(() =>
            ContentPostGenerationValidator.ValidateAndNormalize(
                result,
                ContentPlatform.LinkedIn));
    }

    [Fact]
    public void Too_many_linkedin_hashtags_are_rejected()
    {
        var result = CreateResult(
            "LinkedIn",
            "Educational",
            ["#one", "#two", "#three", "#four", "#five", "#six"]);

        Assert.Throws<InvalidAiResponseException>(() =>
            ContentPostGenerationValidator.ValidateAndNormalize(
                result,
                ContentPlatform.LinkedIn));
    }

    [Fact]
    public void Edited_content_is_normalized_with_the_same_rules()
    {
        var result = ContentPostGenerationValidator.ValidateEdited(
            CreateResult("LinkedIn", "Educational", ["AI", "#viral", "#ai", "SoftwareEngineering"]),
            ContentPlatform.LinkedIn);

        Assert.Equal(["#AI", "#SoftwareEngineering"], result.Hashtags);
        Assert.StartsWith("A useful opening", result.Content);
    }

    [Fact]
    public void Edited_content_reports_a_user_facing_failure_instead_of_an_ai_failure()
    {
        var result = CreateResult("TikTok", "Educational", []);

        var exception = Assert.Throws<InvalidContentPostContentException>(() =>
            ContentPostGenerationValidator.ValidateEdited(
                result,
                ContentPlatform.LinkedIn));

        Assert.Equal("The content post contained an invalid platform.", exception.Message);
    }

    [Fact]
    public void Edited_x_content_over_the_limit_is_rejected()
    {
        var result = CreateResult("X", "Insight", []);
        result = new ContentPostGenerationResult
        {
            Platform = result.Platform,
            Content = new string('x', 281),
            Hook = result.Hook,
            CallToAction = result.CallToAction,
            Hashtags = result.Hashtags,
            ContentType = result.ContentType,
            StorytellingStructure = result.StorytellingStructure
        };

        Assert.Throws<InvalidContentPostContentException>(() =>
            ContentPostGenerationValidator.ValidateEdited(
                result,
                ContentPlatform.X));
    }

    [Fact]
    public void Ai_validation_still_reports_an_ai_failure()
    {
        var result = CreateResult("TikTok", "Educational", []);

        var exception = Assert.Throws<InvalidAiResponseException>(() =>
            ContentPostGenerationValidator.ValidateAndNormalize(
                result,
                ContentPlatform.LinkedIn));

        Assert.Equal("The AI response contained an invalid platform.", exception.Message);
    }

    private static ContentPostGenerationResult CreateResult(
        string platform,
        string contentType,
        List<string?> hashtags) => new()
    {
        Platform = platform,
        Content = "A useful opening\n\nA practical takeaway.",
        Hook = "A useful opening",
        CallToAction = "What would you add?",
        Hashtags = hashtags,
        ContentType = contentType,
        StorytellingStructure = "Hook → Insight → Takeaway → CTA"
    };
}