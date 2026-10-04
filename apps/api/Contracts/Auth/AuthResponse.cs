namespace Api.Contracts.Auth;

public sealed class AuthResponse
{
    /// <summary>
    /// When the access token in the session cookie stops being accepted. The
    /// token itself is not in the body: it is an httpOnly cookie, so page script
    /// cannot read it and never has to hold it.
    /// </summary>
    public DateTime ExpiresAt { get; init; }

    public UserResponse User { get; init; } = new();

    /// <summary>
    /// The workspace the session is now scoped to. A user with several
    /// workspaces changes this by switching, not by sending a header.
    /// </summary>
    public Guid WorkspaceId { get; init; }
}