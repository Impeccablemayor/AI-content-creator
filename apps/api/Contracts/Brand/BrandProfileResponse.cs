namespace Api.Contracts.Brand;

public sealed class BrandProfileResponse
{
    public Guid Id { get; init; }

    public Guid WorkspaceId { get; init; }

    public string Name { get; init; } = string.Empty;

    public string Description { get; init; } = string.Empty;

    public string Industry { get; init; } = string.Empty;

    public string ToneOfVoice { get; init; } = string.Empty;

    public string ContentGoals { get; init; } = string.Empty;
}