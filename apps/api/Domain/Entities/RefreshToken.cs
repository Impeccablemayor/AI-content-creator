using Api.Domain.Enums;

namespace Api.Domain.Entities;

/// <summary>
/// One issued refresh token, stored only as a SHA-256 hash of the value the
/// client holds.
/// </summary>
/// <remarks>
/// <para>
/// Only the hash is persisted. The plaintext is never written anywhere, and
/// nothing in the system can reproduce it, so a database disclosure does not
/// yield usable credentials. Lookup is by hash, which is all the refresh flow
/// needs, so there is deliberately no second reversible copy of the value.
/// </para>
/// <para>
/// Rows form a chain. Rotation creates a new row and points the consumed row's
/// <see cref="ReplacedById"/> at it, which makes every rotation generation
/// traceable and lets a replayed token be walked forward to the session it
/// spawned. <see cref="ReplacedById"/> being null on a revoked row is what
/// distinguishes a logout revocation from a consumed-by-rotation token.
/// </para>
/// <para>
/// The row carries both the user and the workspace it was issued for. Refresh
/// reads the workspace from here rather than from the request, so a token can
/// never be redeemed against a workspace it was not issued for.
/// </para>
/// </remarks>
public sealed class RefreshToken
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public Guid WorkspaceId { get; set; }

    /// <summary>Base64 SHA-256 of the issued token. Unique; the lookup key.</summary>
    public string TokenHash { get; set; } = string.Empty;

    public DateTime ExpiresAt { get; set; }

    public DateTime? RevokedAt { get; set; }

    /// <summary>
    /// The token issued when this one was rotated. Null while this token is
    /// active, and null for a token revoked by logout.
    /// </summary>
    public Guid? ReplacedById { get; set; }

    public string? RevocationReason { get; set; }

    public RefreshTokenStatus Status { get; set; } = RefreshTokenStatus.Active;

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public User User { get; set; } = null!;

    public Workspace Workspace { get; set; } = null!;
}
