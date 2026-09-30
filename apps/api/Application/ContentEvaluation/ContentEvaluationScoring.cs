using Api.Application.AI.Models;
using Api.Domain.Enums;

namespace Api.Application.ContentEvaluation;

public static class ContentEvaluationScoring
{
    public const decimal AcceptThreshold = 90m;
    public const decimal MinorEditThreshold = 80m;
    public const decimal ConsiderImprovementThreshold = 70m;
    public const decimal ReviseThreshold = 60m;

    public static decimal CalculateOverallScore(ContentEvaluationResult result) =>
        Math.Round(
            (result.StrategicFit!.Value
                + result.AudienceFit!.Value
                + result.HookStrength!.Value
                + result.Value!.Value
                + result.Clarity!.Value
                + result.Originality!.Value
                + result.Storytelling!.Value
                + result.PlatformFit!.Value
                + result.VoiceMatch!.Value
                + result.FactualReliability!.Value) / 10m,
            2,
            MidpointRounding.AwayFromZero);

    public static ContentEvaluationRecommendation Recommend(decimal overallScore)
    {
        if (overallScore >= AcceptThreshold)
        {
            return ContentEvaluationRecommendation.Accept;
        }

        if (overallScore >= MinorEditThreshold)
        {
            return ContentEvaluationRecommendation.MinorEdit;
        }

        if (overallScore >= ConsiderImprovementThreshold)
        {
            return ContentEvaluationRecommendation.ConsiderImprovement;
        }

        return overallScore >= ReviseThreshold
            ? ContentEvaluationRecommendation.Revise
            : ContentEvaluationRecommendation.Rewrite;
    }

    public static bool Passes(decimal overallScore, decimal passThreshold) =>
        overallScore >= passThreshold;
}
