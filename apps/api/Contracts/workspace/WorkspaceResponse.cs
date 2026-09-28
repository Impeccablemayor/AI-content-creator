namespace Api.Contracts.Workspace;

public sealed class WorkspaceResponse
{
    public Guid Id { get; init; }

    public string Name { get; init; } = string.Empty;

    public string Status { get; init; } = string.Empty;

    public string Role { get; init; } = string.Empty;
}