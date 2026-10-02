namespace Api.Domain.Enums;

/// <summary>
/// The lifecycle state of a refresh token.
/// </summary>
public enum RefreshTokenStatus
{
    /// <summary>Issued and not yet used, revoked, or expired.</summary>
    Active = 1,

    /// <summary>Past its expiry, or observed past its expiry.</summary>
    Expired = 2,

    /// <summary>
    /// No longer usable. Set by rotation and by logout. Paired with a null
    /// <c>ReplacedById</c> this means the holder ended the session; paired with a
    /// replacement it means the token was consumed by a rotation.
    /// </summary>
    Revoked = 3,

    /// <summary>
    /// A token already consumed by rotation was presented again. This is the
    /// reuse signal: either the client replayed a stale token, or the token was
    /// stolen. Either way the descendant session is revoked.
    /// </summary>
    Reused = 4
}
