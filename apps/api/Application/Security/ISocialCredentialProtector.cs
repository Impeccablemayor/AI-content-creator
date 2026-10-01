namespace Api.Application.Security;

/// <summary>
/// Encrypts and decrypts social platform OAuth credentials at rest.
/// </summary>
/// <remarks>
/// Access and refresh tokens are protected under separate purposes so a value
/// protected for one can never be unprotected as the other. Implementations
/// must never log, return, or otherwise surface the plaintext value.
/// </remarks>
public interface ISocialCredentialProtector
{
    string ProtectAccessToken(string accessToken);

    string ProtectRefreshToken(string refreshToken);

    string UnprotectAccessToken(string protectedAccessToken);

    string UnprotectRefreshToken(string protectedRefreshToken);
}
