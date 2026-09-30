using Api.Application.AI.Models;

namespace Api.Application.ContentEvaluation;

public static class ContentEvaluationValidator
{
    public const int MaxListLength = 5;

    private const int MinDimensionScore = 0;
    private const int MaxDimensionScore = 100;

    public static ContentEvaluationResult ValidateAndNormalize(ContentEvaluationResult? result)
    {
        if (result is null)
        {
            throw Failure("The AI evaluation response was empty.");
        }

        var dimensions = new (string Name, decimal? Value)[]
        {
            ("strategicFit", result.StrategicFit),
            ("audienceFit", result.AudienceFit),
            ("hookStrength", result.HookStrength),
            ("value", result.Value),
            ("clarity", result.Clarity),
            ("originality", result.Originality),
            ("storytelling", result.Storytelling),
            ("platformFit", result.PlatformFit),
            ("voiceMatch", result.VoiceMatch),
            ("factualReliability", result.FactualReliability)
        };

        foreach (var (name, value) in dimensions)
        {
            if (value is null)
            {
                throw Failure($"The AI evaluation omitted the {name} dimension.");
            }

            if (value < MinDimensionScore || value > MaxDimensionScore)
            {
                throw Failure($"The AI evaluation returned an out-of-range {name} score.");
            }
        }

        return new ContentEvaluationResult
        {
            StrategicFit = result.StrategicFit,
            AudienceFit = result.AudienceFit,
            HookStrength = result.HookStrength,
            Value = result.Value,
            Clarity = result.Clarity,
            Originality = result.Originality,
            Storytelling = result.Storytelling,
            PlatformFit = result.PlatformFit,
            VoiceMatch = result.VoiceMatch,
            FactualReliability = result.FactualReliability,
            Strengths = NormalizeList(result.Strengths, "strengths"),
            Issues = NormalizeList(result.Issues, "issues")
        };
    }

    private static List<string> NormalizeList(List<string>? values, string name)
    {
        if (values is null)
        {
            throw Failure($"The AI evaluation omitted the {name} list.");
        }

        if (values.Count > MaxListLength)
        {
            throw Failure($"The AI evaluation returned too many {name}.");
        }

        var normalized = new List<string>(values.Count);
        foreach (var value in values)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw Failure($"The AI evaluation returned an empty {name} entry.");
            }

            normalized.Add(value.Trim());
        }

        return normalized;
    }

    private static ContentEvaluationFailedException Failure(string message) =>
        new($"The AI evaluation was unusable. {message}");
}
