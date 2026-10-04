using Api.Configuration;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace Api.Application.Authentication;

/// <summary>
/// Reads and writes the browser session cookies.
/// </summary>
public interface IAuthCookieService
{
    /// <summary>
    /// Replaces both session cookies with the supplied credentials. Called on
    /// every successful authentication, because each of them issues a new pair.
    /// </summary>
    void Write(
        HttpContext context,
        string accessToken,
        string refreshToken,
        DateTime accessTokenExpiresAt);

    /// <summary>
    /// Deletes both session cookies. Called on logout, and on any authentication
    /// failure that leaves a stale cookie behind.
    /// </summary>
    void Clear(HttpContext context);

    /// <summary>The refresh token the browser presented, if any.</summary>
    string? ReadRefreshToken(HttpContext context);
}

/// <summary>
/// The browser's copy of the session.
/// </summary>
/// <remarks>
/// <para>
/// The refresh token exists in exactly one place on the client: this cookie, and
/// it is httpOnly, so page script cannot read it and a cross-site script
/// injection cannot exfiltrate it. That is the whole point of the type. The
/// access token is httpOnly for the same reason, which is why
/// <see cref="Program"/> also hands it to the bearer pipeline from the cookie.
/// </para>
/// <para>
/// Writing replaces rather than appends. Every successful authentication
/// rotates the refresh token, so a stale value left in the browser would be a
/// live credential that the server has already revoked, and the next refresh
/// would report it as a replay and kill the session the user is actually using.
/// </para>
/// <para>
/// Clearing is not conditional on the cookie's attributes being identical to the
/// ones it was written with. A browser matches a deletion on name, domain, and
/// path, and SameSite and Secure are not part of that match, so an expired copy
/// carrying the written values is enough to remove it. An expired cookie is
/// still sent back by a client, so logout cannot simply ignore the cookie
/// either.
/// </para>
/// </remarks>
public sealed class AuthCookieService : IAuthCookieService
{
    private readonly AuthCookieOptions _options;
    private readonly JwtOptions _jwtOptions;

    public AuthCookieService(
        IOptions<AuthCookieOptions> options,
        IOptions<JwtOptions> jwtOptions)
    {
        _options = options.Value;
        _jwtOptions = jwtOptions.Value;
    }

    public void Write(
        HttpContext context,
        string accessToken,
        string refreshToken,
        DateTime accessTokenExpiresAt)
    {
        context.Response.Cookies.Append(
            _options.AccessTokenCookieName,
            accessToken,
            BuildOptions(
                new DateTimeOffset(
                    DateTime.SpecifyKind(accessTokenExpiresAt, DateTimeKind.Utc))));

        context.Response.Cookies.Append(
            _options.RefreshTokenCookieName,
            refreshToken,
            BuildOptions(
                DateTimeOffset.UtcNow.AddDays(
                    _jwtOptions.RefreshTokenExpirationDays)));
    }

    public void Clear(HttpContext context)
    {
        context.Response.Cookies.Delete(
            _options.AccessTokenCookieName,
            BuildOptions(DateTimeOffset.UnixEpoch));

        context.Response.Cookies.Delete(
            _options.RefreshTokenCookieName,
            BuildOptions(DateTimeOffset.UnixEpoch));
    }

    public string? ReadRefreshToken(HttpContext context)
    {
        var value = context.Request.Cookies[
            _options.RefreshTokenCookieName];

        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    private CookieOptions BuildOptions(DateTimeOffset expiresAt) =>
        new()
        {
            HttpOnly = true,

            // A session cookie with no explicit lifetime is discarded when the
            // browser closes, which is not what a "remember me" login means.
            Expires = expiresAt,

            Path = _options.Path,
            Domain = string.IsNullOrWhiteSpace(_options.Domain)
                ? null
                : _options.Domain,
            Secure = _options.Secure,
            SameSite = _options.SameSite,

            // Exempts the cookie from browser rules that could suppress it on a
            // reload or a background request. Without it a cookie can be dropped
            // from a request that the user made no navigation for, which reads
            // to the application as a logged-out user.
            IsEssential = true
        };
}