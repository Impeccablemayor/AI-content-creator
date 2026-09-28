using Api.Application.AI.Models;

namespace Api.Application.ContentIdeas;

public static class ContentIdeaGenerationValidator
{
    public static IReadOnlyList<GeneratedContentIdea> ValidateAndNormalize(
        ContentIdeaGenerationResult? result,
        int expectedCount)
    {
        if (result?.Ideas is null || result.Ideas.Count != expectedCount)
        {
            throw new InvalidAiResponseException(
                "The AI response did not contain the requested number of ideas.");
        }

        var normalized = new List<GeneratedContentIdea>(expectedCount);

        foreach (var idea in result.Ideas)
        {
            if (idea is null ||
                !HasText(idea.Title, 300) ||
                !HasText(idea.Topic, 500) ||
                !HasText(idea.Angle, 1000) ||
                !HasText(idea.Hook, 1000) ||
                !HasText(idea.Reason, 2000) ||
                idea.Scores is null)
            {
                throw new InvalidAiResponseException(
                    "The AI response contains missing or oversized required content.");
            }

            var hashtags = idea.Hashtags ?? [];
            if (hashtags.Count > 20 || hashtags.Any(
                    hashtag => hashtag is null || hashtag.Length > 100))
            {
                throw new InvalidAiResponseException(
                    "The AI response contains invalid hashtags.");
            }

            normalized.Add(new GeneratedContentIdea
            {
                Title = idea.Title!.Trim(),
                Topic = idea.Topic!.Trim(),
                Angle = idea.Angle!.Trim(),
                Hook = idea.Hook!.Trim(),
                Reason = idea.Reason!.Trim(),
                Hashtags = hashtags
                    .Where(hashtag => !string.IsNullOrWhiteSpace(hashtag))
                    .Select(hashtag => hashtag!.Trim())
                    .ToList(),
                Scores = new GeneratedContentIdeaScores
                {
                    Relevance = Clamp(idea.Scores.Relevance),
                    EngagementPotential =
                        Clamp(idea.Scores.EngagementPotential),
                    BrandFit = Clamp(idea.Scores.BrandFit)
                }
            });
        }

        return normalized;
    }

    private static bool HasText(string? value, int maxLength) =>
        !string.IsNullOrWhiteSpace(value) && value.Trim().Length <= maxLength;

    private static decimal Clamp(decimal score) =>
        Math.Clamp(score, 0m, 100m);
}