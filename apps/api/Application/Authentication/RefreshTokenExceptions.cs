namespace Api.Application.Authentication;

/// <summary>
/// Thrown when the presented refresh token does not match any issued token.
/// </summary>
public sealed class InvalidRefreshTokenException : Exception
{
    public InvalidRefreshTokenException()
        : base("The refresh token is not valid.")
    {
    }
}

/// <summary>
/// Thrown when a refresh token is past its expiry.
/// </summary>
public sealed class ExpiredRefreshTokenException : Exception
{
    public ExpiredRefreshTokenException()
        : base("The refresh token has expired.")
    {
    }
}

/// <summary>
/// Thrown when a refresh token was revoked without being rotated, which is what
/// logout does.
/// </summary>
public sealed class RevokedRefreshTokenException : Exception
{
    public RevokedRefreshTokenException()
        : base("The refresh token has been revoked.")
    {
    }
}

/// <summary>
/// Thrown when a refresh token that was already consumed by a rotation is
/// presented again. The descendant session has been revoked by the time this
/// surfaces.
/// </summary>
public sealed class ReusedRefreshTokenException : Exception
{
    public ReusedRefreshTokenException()
        : base(
            "The refresh token was already used. " +
            "All sessions started from it have been revoked.")
    {
    }
}

/// <summary>
/// Thrown when another request rotated the same token first. The caller lost
/// the race; it is not treated as reuse, because a double submit from one
/// client is indistinguishable from an attack only by guessing.
/// </summary>
public sealed class RefreshTokenRotationConflictException : Exception
{
    public RefreshTokenRotationConflictException()
        : base("The refresh token was already rotated by another request.")
    {
    }
}

/// <summary>
/// Thrown when the account behind a valid refresh token can no longer use it.
/// </summary>
public sealed class RefreshTokenOwnerInactiveException : Exception
{
    public RefreshTokenOwnerInactiveException()
        : base("This account is inactive.")
    {
    }
}

/// <summary>
/// Thrown when the user behind a valid refresh token is no longer a member of
/// the workspace the token was issued for.
/// </summary>
public sealed class RefreshTokenWorkspaceAccessRevokedException : Exception
{
    public RefreshTokenWorkspaceAccessRevokedException()
        : base(
            "This account no longer has access to the workspace " +
            "the refresh token was issued for.")
    {
    }
}
