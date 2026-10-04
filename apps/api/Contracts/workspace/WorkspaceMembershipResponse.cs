namespace Api.Contracts.Workspace;

/// <summary>
/// One workspace the authenticated user belongs to, as shown in a workspace
/// switcher.
/// </summary>
/// <remarks>
/// Separate from <see cref="WorkspaceResponse"/> because the list is a
/// membership projection: it is scoped to the caller rather than to the current
/// session, so it carries which workspace is active and is never paginated over
/// workspaces the caller cannot see.
/// </remarks>
public sealed class WorkspaceMembershipResponse
{
    public Guid Id { get; init; }

    public string Name { get; init; } = string.Empty;

    public string Status { get; init; } = string.Empty;

    public string Role { get; init; } = string.Empty;

    /// <summary>
    /// Whether the current session is scoped to this workspace. Exactly one
    /// entry in the list has this set, because the session is scoped to exactly
    /// one workspace.
    /// </summary>
    public bool IsCurrent { get; init; }
}