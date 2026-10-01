using Api.Application.AI.Models;

namespace Api.Application.Learning;

public sealed class LearningInsightFailedException : Exception
{
    public LearningInsightFailedException(string message) : base(message)
    {
    }
}

/// <summary>
/// Validates the model's answer before it is stored next to the measurement it
/// was based on.
/// </summary>
public static class LearningInsightValidator
{
    /// <summary>
    /// Upper bound on a recommendation. Long enough for a concrete next step,
    /// short enough that the stored signal stays a signal rather than a report
    /// nobody reads.
    /// </summary>
    public const int MaxRecommendationLength = 500;

    public static string ValidateAndNormalize(LearningInsightResult? result)
    {
        if (result is null)
        {
            throw Failure("The AI response was empty.");
        }

        if (string.IsNullOrWhiteSpace(result.Recommendation))
        {
            throw Failure("The AI response omitted the recommendation.");
        }

        var recommendation = result.Recommendation.Trim();

        if (recommendation.Length > MaxRecommendationLength)
        {
            throw Failure(
                $"The AI recommendation was {recommendation.Length} characters, over the " +
                $"{MaxRecommendationLength} allowed.");
        }

        return recommendation;
    }

    private static LearningInsightFailedException Failure(string message) =>
        new($"The AI recommendation was unusable. {message}");
}
