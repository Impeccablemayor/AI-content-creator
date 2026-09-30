using System.Text;
using Api.Application.AI.Models;
using Api.Application.ContentIdeas;
using Api.Domain.Enums;

namespace Api.Application.ContentPosts;

public static class ContentPostGenerationValidator
{
    private const string AiSubject = "The AI response";
    private const string EditSubject = "The content post";

    private static readonly HashSet<string> GenericHashtags = new(StringComparer.OrdinalIgnoreCase)
    {
        "#viral", "#fyp", "#trending"
    };

    public static ContentPostGenerationResult ValidateAndNormalize(
        ContentPostGenerationResult? result,
        ContentPlatform expectedPlatform) =>
        Validate(
            result,
            expectedPlatform,
            AiSubject,
            static message => new InvalidAiResponseException(message));

    public static ContentPostGenerationResult ValidateEdited(
        ContentPostGenerationResult? result,
        ContentPlatform expectedPlatform) =>
        Validate(
            result,
            expectedPlatform,
            EditSubject,
            static message => new InvalidContentPostContentException(message));

    private static ContentPostGenerationResult Validate(
        ContentPostGenerationResult? result,
        ContentPlatform expectedPlatform,
        string subject,
        Func<string, Exception> createException)
    {
        if (result is null ||
            !Enum.TryParse<ContentPlatform>(result.Platform, true, out var platform) ||
            !Enum.IsDefined(platform) ||
            platform != expectedPlatform)
        {
            throw Failure(createException, subject, "an invalid platform");
        }

        if (!Enum.TryParse<ContentType>(result.ContentType, true, out var contentType) ||
            !Enum.IsDefined(contentType))
        {
            throw Failure(createException, subject, "an invalid content type");
        }

        var maxContentLength = platform == ContentPlatform.X ? 280 : 3000;
        var maxHashtags = platform == ContentPlatform.X ? 3 : 5;
        if (!HasText(result.Content) || CountRunes(result.Content!) > maxContentLength)
        {
            throw Failure(createException, subject, "missing or oversized post content");
        }

        if (!HasText(result.Hook, 300) ||
            result.CallToAction is null || result.CallToAction.Trim().Length > 300 ||
            !HasText(result.StorytellingStructure, 300))
        {
            throw Failure(createException, subject, "missing or oversized post metadata");
        }

        if (result.Hashtags is null || result.Hashtags.Count > maxHashtags)
        {
            throw Failure(createException, subject, "an invalid number of hashtags");
        }

        var hashtags = new List<string?>(result.Hashtags.Count);
        foreach (var rawHashtag in result.Hashtags)
        {
            if (rawHashtag is null || rawHashtag.Trim().Length > 100)
            {
                throw Failure(createException, subject, "an invalid hashtag");
            }

            var value = rawHashtag.Trim().TrimStart('#');
            if (value.Length == 0 || !IsValidHashtag(value))
            {
                throw Failure(createException, subject, "an invalid hashtag");
            }

            var normalized = $"#{value}";
            if (!GenericHashtags.Contains(normalized) &&
                !hashtags.Contains(normalized, StringComparer.OrdinalIgnoreCase))
            {
                hashtags.Add(normalized);
            }
        }

        return new ContentPostGenerationResult
        {
            Platform = platform.ToString(),
            Content = result.Content!.Trim(),
            Hook = result.Hook!.Trim(),
            CallToAction = result.CallToAction.Trim(),
            Hashtags = hashtags,
            ContentType = contentType.ToString(),
            StorytellingStructure = result.StorytellingStructure!.Trim()
        };
    }

    private static Exception Failure(
        Func<string, Exception> createException,
        string subject,
        string problem) =>
        createException($"{subject} contained {problem}.");

    private static bool HasText(string? value, int maxLength = int.MaxValue) =>
        !string.IsNullOrWhiteSpace(value) && value.Trim().Length <= maxLength;

    private static bool IsValidHashtag(string value)
    {
        foreach (var rune in value.EnumerateRunes())
        {
            if (!Rune.IsLetterOrDigit(rune) && rune.Value != '_')
            {
                return false;
            }
        }

        return true;
    }

    private static int CountRunes(string value)
    {
        var count = 0;
        foreach (var _ in value.EnumerateRunes())
        {
            count++;
        }

        return count;
    }
}
