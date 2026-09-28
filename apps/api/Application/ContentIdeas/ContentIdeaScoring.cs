namespace Api.Application.ContentIdeas;

public static class ContentIdeaScoring
{
    public const decimal NeutralFreshnessScore = 50m;

    public static decimal Calculate(
        decimal relevanceScore,
        decimal engagementPotentialScore,
        decimal brandFitScore)
    {
        var score = relevanceScore * 0.35m
            + engagementPotentialScore * 0.30m
            + brandFitScore * 0.25m
            + NeutralFreshnessScore * 0.10m;

        return Math.Round(Math.Clamp(score, 0m, 100m), 2);
    }
}