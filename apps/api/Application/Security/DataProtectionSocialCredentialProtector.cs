using Microsoft.AspNetCore.DataProtection;

namespace Api.Application.Security;

/// <summary>
/// Protects social credentials with ASP.NET Core Data Protection.
/// </summary>
/// <remarks>
/// The mechanism is intentionally hidden behind
/// <see cref="ISocialCredentialProtector"/> so the backing store can be
/// replaced (for example with a KMS-backed envelope key) without changing
/// SocialAccount business logic.
/// </remarks>
public sealed class DataProtectionSocialCredentialProtector : ISocialCredentialProtector
{
    public const string AccessTokenPurpose =
        "Mayorx.ContentEngine.SocialCredentials.AccessToken.v1";

    public const string RefreshTokenPurpose =
        "Mayorx.ContentEngine.SocialCredentials.RefreshToken.v1";

    private readonly IDataProtector _accessTokenProtector;
    private readonly IDataProtector _refreshTokenProtector;

    public DataProtectionSocialCredentialProtector(IDataProtectionProvider provider)
    {
        ArgumentNullException.ThrowIfNull(provider);

        _accessTokenProtector = provider.CreateProtector(AccessTokenPurpose);
        _refreshTokenProtector = provider.CreateProtector(RefreshTokenPurpose);
    }

    public string ProtectAccessToken(string accessToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(accessToken);

        return _accessTokenProtector.Protect(accessToken);
    }

    public string ProtectRefreshToken(string refreshToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(refreshToken);

        return _refreshTokenProtector.Protect(refreshToken);
    }

    public string UnprotectAccessToken(string protectedAccessToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(protectedAccessToken);

        return _accessTokenProtector.Unprotect(protectedAccessToken);
    }

    public string UnprotectRefreshToken(string protectedRefreshToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(protectedRefreshToken);

        return _refreshTokenProtector.Unprotect(protectedRefreshToken);
    }
}
