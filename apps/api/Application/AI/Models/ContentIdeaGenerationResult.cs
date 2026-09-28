namespace Api.Application.AI.Models;

public sealed class ContentIdeaGenerationResult
{
    public List<GeneratedContentIdea?>? Ideas { get; init; }
}

public sealed class GeneratedContentIdea
{
    public string? Title { get; init; }

    public string? Topic { get; init; }

    public string? Angle { get; init; }

    public string? Hook { get; init; }

    public string? Reason { get; init; }

    public List<string>? Hashtags { get; init; }

    public GeneratedContentIdeaScores? Scores { get; init; }
}

public sealed class GeneratedContentIdeaScores
{
    public decimal Relevance { get; init; }

    public decimal EngagementPotential { get; init; }

    public decimal BrandFit { get; init; }
}