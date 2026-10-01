using Api.Domain.Enums;

namespace Api.Domain.Entities;

public sealed class SocialAccount
{
    public Guid Id { get; set; }

    public Guid WorkspaceId { get; set; }

    public ContentPlatform Platform { get; set; }

    public string PlatformAccountId { get; set; } = string.Empty;

    public string? Username { get; set; }

    public string? DisplayName { get; set; }

    public string? ProfileUrl { get; set; }

    public string? AccessTokenEncrypted { get; set; }

    public string? RefreshTokenEncrypted { get; set; }

    public DateTime? TokenExpiresAt { get; set; }

    public List<string> Scopes { get; set; } = [];

    public SocialAccountStatus Status { get; set; } = SocialAccountStatus.Connected;

    public DateTime ConnectedAt { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public Workspace Workspace { get; set; } = null!;
}
