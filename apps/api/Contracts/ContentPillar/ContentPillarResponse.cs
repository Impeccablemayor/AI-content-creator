namespace Api.Contracts.ContentPillar;

public sealed class ContentPillarResponse
{
    public Guid Id { get; init; }

    public string Name { get; init; } = string.Empty;

    public string Description { get; init; } = string.Empty;

    public string? Objective { get; init; }

    /// <summary>
    /// The audience profile this pillar targets, when one is set.
    /// </summary>
    public Guid? AudienceProfileId { get; init; }

    /// <summary>
    /// How often this pillar should appear relative to the workspace's other
    /// pillars, as a percentage in (0, 100].
    /// </summary>
    public decimal Weight { get; init; }

    public bool IsActive { get; init; }

    /// <summary>
    /// How many ideas are currently attributed to this pillar.
    /// </summary>
    public int IdeaCount { get; init; }

    public DateTime CreatedAt { get; init; }

    public DateTime UpdatedAt { get; init; }
}

public sealed class CreateContentPillarRequest
{
    public string? Name { get; init; }

    public string? Description { get; init; }

    public string? Objective { get; init; }

    public Guid? AudienceProfileId { get; init; }

    public decimal? Weight { get; init; }

    public bool? IsActive { get; init; }
}

/// <summary>
/// A partial update. Every field is optional; omitted fields are left as they
/// are, so a caller can rename a pillar without resending its weight.
/// </summary>
public sealed class UpdateContentPillarRequest
{
    public string? Name { get; init; }

    public string? Description { get; init; }

    public string? Objective { get; init; }

    public Guid? AudienceProfileId { get; init; }

    public decimal? Weight { get; init; }

    public bool? IsActive { get; init; }
}
