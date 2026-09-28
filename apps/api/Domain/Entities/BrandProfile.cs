namespace Api.Domain.Entities;

public sealed class BrandProfile
{
    public Guid Id { get; set; }

    public Guid WorkspaceId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public string Industry { get; set; } = string.Empty;

    public string ToneOfVoice { get; set; } = string.Empty;

    public string ContentGoals { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public Workspace Workspace { get; set; } = null!;
}