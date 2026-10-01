namespace Api.Domain.Entities;

/// <summary>
/// A recurring content theme that content ideas can be attributed to.
/// </summary>
/// <remarks>
/// This is the entity the design calls `content_pillars`. It did not exist
/// before: <see cref="ContentStrategy.ContentPillars"/> is a free-text string,
/// which cannot be referenced by an idea, filtered on, or reported against.
/// </remarks>
public sealed class ContentPillar
{
    public Guid Id { get; set; }

    public Guid WorkspaceId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// What this pillar is meant to achieve. Optional.
    /// </summary>
    public string? Objective { get; set; }

    /// <summary>
    /// The audience profile this pillar is aimed at. Optional, and must belong
    /// to the same workspace when set.
    /// </summary>
    public Guid? TargetAudienceId { get; set; }

    /// <summary>
    /// How frequently this pillar should appear relative to the workspace's
    /// other pillars, as a percentage in (0, 100].
    /// </summary>
    public decimal Weight { get; set; }

    /// <summary>
    /// Whether the pillar is still in use. Deactivated pillars keep their ideas
    /// but stop being offered as new themes.
    /// </summary>
    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public ICollection<ContentIdea> ContentIdeas { get; set; }
        = new List<ContentIdea>();

    public Workspace Workspace { get; set; } = null!;
}
