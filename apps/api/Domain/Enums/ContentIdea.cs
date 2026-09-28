using Api.Domain.Enums;

namespace Api.Domain.Entities;

public sealed class ContentIdea
{
    public Guid Id { get; set; }

    public Guid WorkspaceId { get; set; }

    public ContentIdeaStatus Status { get; set; }
        = ContentIdeaStatus.Suggested;

    public ContentPlatform Platform { get; set; }

    public string Title { get; set; } = string.Empty;

    public string Topic { get; set; } = string.Empty;

    public string Angle { get; set; } = string.Empty;

    public string Hook { get; set; } = string.Empty;

    public string Reason { get; set; } = string.Empty;

    public string SuggestedHashtags { get; set; } = string.Empty;

    public decimal RelevanceScore { get; set; }

    public decimal EngagementPotentialScore { get; set; }

    public decimal BrandFitScore { get; set; }

    public decimal OverallScore { get; set; }

    public DateTime? SuggestedPublishAt { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public Workspace Workspace { get; set; } = null!;
}