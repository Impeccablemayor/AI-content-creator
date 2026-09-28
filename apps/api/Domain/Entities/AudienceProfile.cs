namespace Api.Domain.Entities;

public sealed class AudienceProfile
{
    public Guid Id { get; set; }

    public Guid WorkspaceId { get; set; }

    public string Description { get; set; } = string.Empty;

    public string Demographics { get; set; } = string.Empty;

    public string Interests { get; set; } = string.Empty;

    public string PainPoints { get; set; } = string.Empty;

    public string Goals { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public Workspace Workspace { get; set; } = null!;
}