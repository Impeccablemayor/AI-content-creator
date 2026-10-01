namespace Api.Domain.Enums;

/// <summary>
/// Why the last publish attempt for a scheduled post did not succeed.
/// </summary>
/// <remarks>
/// A failure never changes the post's status. The post stays
/// <see cref="ContentPostStatus.Scheduled"/> and is retried when its claim
/// expires, so this records *why* the last attempt failed rather than acting as
/// a new lifecycle state.
/// </remarks>
public enum PublishFailureReason
{
    /// <summary>The platform has no publisher implementation.</summary>
    PlatformNotSupported = 1,

    /// <summary>The platform has no client credentials configured.</summary>
    PlatformNotConfigured = 2,

    /// <summary>No social account is connected for the post's platform.</summary>
    SocialAccountNotFound = 3,

    /// <summary>The connected account is disconnected, revoked, or in error.</summary>
    SocialAccountNotConnected = 4,

    /// <summary>
    /// More than one usable account is connected for the platform, so the
    /// account to publish as is ambiguous.
    /// </summary>
    SocialAccountAmbiguous = 5,

    /// <summary>No access token is stored, or it could not be decrypted.</summary>
    CredentialsUnavailable = 6,

    /// <summary>The stored access token is past its expiry.</summary>
    CredentialsExpired = 7,

    /// <summary>The platform rejected the stored credentials.</summary>
    CredentialsRejected = 8,

    /// <summary>The platform returned an unexpected HTTP status.</summary>
    ProviderUnavailable = 9,

    /// <summary>The platform returned a success response we cannot interpret.</summary>
    ProviderResponseInvalid = 10,

    /// <summary>
    /// The post was no longer claimable at finalisation, so another worker owns
    /// its publish.
    /// </summary>
    PublishClaimLost = 11,

    /// <summary>The post was not in a publishable state when the attempt ran.</summary>
    ContentPostNotScheduled = 12
}
