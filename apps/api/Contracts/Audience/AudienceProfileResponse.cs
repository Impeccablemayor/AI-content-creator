namespace Api.Contracts.Audience;

public sealed class AudienceProfileResponse
{
    public Guid Id { get; init; }

    public Guid WorkspaceId { get; init; }

    public string Description { get; init; } = string.Empty;

    public string Demographics { get; init; } = string.Empty;

    public string Interests { get; init; } = string.Empty;

    public string PainPoints { get; init; } = string.Empty;

    public string Goals { get; init; } = string.Empty;
}