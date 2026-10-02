namespace Api.Contracts.Auth;

public sealed class AuthResponse
{
    public string AccessToken { get; init; } = string.Empty;

    public DateTime ExpiresAt { get; init; }

    public UserResponse User { get; init; } = new();

    public Guid WorkspaceId { get; init; }

    public string RefreshToken { get; init; } = string.Empty;
}