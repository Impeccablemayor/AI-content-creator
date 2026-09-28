using Api.Domain.Enums;

namespace Api.Domain.Entities;

public sealed class Workspace
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public WorkspaceStatus Status { get; set; } = WorkspaceStatus.Active;

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public ICollection<WorkspaceMember> Members { get; set; }
    = new List<WorkspaceMember>();
    public ICollection<ContentIdea> ContentIdeas { get; set; }
    = new List<ContentIdea>();

    public ContentStrategy? ContentStrategy { get; set; }
    public BrandProfile? BrandProfile { get; set; }

    public AudienceProfile? AudienceProfile { get; set; }
}