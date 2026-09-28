using Api.Domain.Enums;

namespace Api.Contracts.ContentIdea;

public sealed class ContentIdeaResponse
{
    public Guid Id { get; init; }

    public string Status { get; init; } = string.Empty;

    public string Platform { get; init; } = string.Empty;

    public string Title { get; init; } = string.Empty;

    public string Topic { get; init; } = string.Empty;

    public string Angle { get; init; } = string.Empty;

    public string Hook { get; init; } = string.Empty;

    public string Reason { get; init; } = string.Empty;

    public string SuggestedHashtags { get; init; } = string.Empty;

    public decimal RelevanceScore { get; init; }

    public decimal EngagementPotentialScore { get; init; }

    public decimal BrandFitScore { get; init; }

    public decimal OverallScore { get; init; }

    public DateTime? SuggestedPublishAt { get; init; }

    public DateTime CreatedAt { get; init; }
}