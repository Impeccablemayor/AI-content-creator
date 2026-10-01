namespace Api.Application.SocialAccounts;

public sealed class UnsupportedSocialPlatformException : Exception
{
    public UnsupportedSocialPlatformException(string message) : base(message)
    {
    }
}

public sealed class SocialPlatformNotConfiguredException : Exception
{
    public SocialPlatformNotConfiguredException(string message) : base(message)
    {
    }
}

public sealed class InvalidSocialOAuthStateException : Exception
{
    public InvalidSocialOAuthStateException(string message) : base(message)
    {
    }
}

public sealed class InvalidSocialAccountTransitionException : Exception
{
    public InvalidSocialAccountTransitionException(string message) : base(message)
    {
    }
}

public sealed class SocialProviderRequestException : Exception
{
    public SocialProviderRequestException(string message) : base(message)
    {
    }
}

public sealed class SocialProviderAuthenticationException : Exception
{
    public SocialProviderAuthenticationException(string message) : base(message)
    {
    }
}

/// <summary>
/// The platform could not be reached or returned a transient failure.
/// </summary>
/// <remarks>
/// Kept separate from <see cref="SocialProviderRequestException"/> so a platform
/// outage is not recorded as a bad response. Retrying is correct for one and
/// pointless for the other.
/// </remarks>
public sealed class SocialProviderUnavailableException : Exception
{
    public SocialProviderUnavailableException(string message) : base(message)
    {
    }
}
