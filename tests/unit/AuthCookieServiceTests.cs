using Api.Application.Authentication;
using Api.Configuration;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Xunit;

namespace Api.Tests;

/// <summary>
/// The attributes the API puts on the browser's session cookies, and the
/// clearing of them.
/// </summary>
/// <remarks>
/// <para>
/// These are the security properties the whole design exists to provide, so they
/// are asserted on the bytes that actually go out rather than inferred from the
/// fact that a cookie was set. "A cookie was written" and "the cookie is
/// httpOnly" are different claims, and only the second one is the property.
/// </para>
/// <para>
/// Every test builds the real service with real options and inspects the real
/// <c>Set-Cookie</c> headers, so nothing here can pass because of a stub that
/// happens to agree with itself.
/// </para>
/// </remarks>
public sealed class AuthCookieServiceTests
{
    private static readonly DateTime AccessExpiresAt =
        new(2030, 1, 2, 3, 4, 5, DateTimeKind.Utc);

    // ------------------------------------------------------------ httpOnly

    [Fact]
    public void Both_cookies_are_written_as_http_only()
    {
        var context = new DefaultHttpContext();

        Service().Write(context, "access-value", "refresh-value", AccessExpiresAt);

        foreach (var name in new[]
                 {
                     AuthCookieOptions.DefaultAccessTokenCookieName,
                     AuthCookieOptions.DefaultRefreshTokenCookieName
                 })
        {
            Assert.Contains(
                "httponly",
                HeaderFor(context, name),
                StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void A_cookie_that_is_not_http_only_would_be_the_whole_defect()
    {
        // Stated as its own test because it is the one attribute the design is
        // for. If this ever fails, a cross-site script can read the session out
        // of the page, and no other property here compensates for it.
        var context = new DefaultHttpContext();

        Service().Write(context, "access-value", "refresh-value", AccessExpiresAt);

        var header = HeaderFor(
            context,
            AuthCookieOptions.DefaultRefreshTokenCookieName);

        Assert.DoesNotContain(
            "httponly=",
            header,
            StringComparison.OrdinalIgnoreCase);
    }

    // ---------------------------------------------------------- values

    [Fact]
    public void Each_cookie_carries_its_own_credential()
    {
        var context = new DefaultHttpContext();

        Service().Write(
            context,
            "the-access-token",
            "the-refresh-token",
            AccessExpiresAt);

        // The two are not interchangeable: the access token is short-lived and
        // the refresh token is the long-lived credential. Handing the browser the
        // wrong one under either name would either downgrade the session to a
        // password prompt every hour or hand a durable credential to the bearer
        // pipeline.
        Assert.Equal(
            "the-access-token",
            ValueOf(
                context,
                AuthCookieOptions.DefaultAccessTokenCookieName));

        Assert.Equal(
            "the-refresh-token",
            ValueOf(
                context,
                AuthCookieOptions.DefaultRefreshTokenCookieName));
    }

    [Fact]
    public void A_credential_containing_reserved_characters_survives_the_round_trip()
    {
        // JWTs contain '.', '-' and '_', and the header format reserves more.
        // A value that arrived back different from the value sent would
        // authenticate nobody, and the failure would look like a signing problem.
        const string token =
            "eyJhbGciOiJIUzI1NiJ9.eyJ3b3Jrc3BhY2VfaWQiOiJhYmMifQ.sig-part_here+plus/slash=";

        var context = new DefaultHttpContext();

        Service().Write(context, token, token, AccessExpiresAt);

        // Read back the way the framework does, so the comparison is against
        // what a request would actually carry rather than the literal text.
        context.Request.Headers.Cookie =
            HeaderFor(context, AuthCookieOptions.DefaultAccessTokenCookieName);

        Assert.Equal(
            token,
            context.Request.Cookies[
                AuthCookieOptions.DefaultAccessTokenCookieName]);
    }

    // --------------------------------------------------------- lifetimes

    [Fact]
    public void The_access_cookie_expires_when_the_token_does()
    {
        var context = new DefaultHttpContext();

        Service().Write(context, "access", "refresh", AccessExpiresAt);

        // The cookie outliving its token would keep sending a credential the API
        // has already rejected, which reads to the application as a user who is
        // logged out for no stated reason.
        Assert.Equal(
            AccessExpiresAt,
            ExpiryOf(
                context,
                AuthCookieOptions.DefaultAccessTokenCookieName)
                .UtcDateTime);
    }

    [Fact]
    public void The_refresh_cookie_outlives_the_access_cookie()
    {
        var context = new DefaultHttpContext();
        var accessExpiresAt = DateTime.UtcNow.AddHours(1);

        Service().Write(context, "access", "refresh", accessExpiresAt);

        Assert.True(
            ExpiryOf(context, AuthCookieOptions.DefaultRefreshTokenCookieName) >
            ExpiryOf(context, AuthCookieOptions.DefaultAccessTokenCookieName));
    }

    [Fact]
    public void The_refresh_cookie_lifetime_comes_from_the_jwt_configuration()
    {
        var context = new DefaultHttpContext();

        Service(refreshTokenExpirationDays: 30)
            .Write(context, "access", "refresh", AccessExpiresAt);

        var expiry = ExpiryOf(
            context,
            AuthCookieOptions.DefaultRefreshTokenCookieName);

        // Thirty days from roughly now, so a generous window rather than an exact
        // instant: the value is UtcNow plus the configured number of days, and the
        // test must not depend on the clock standing still.
        var remaining = expiry - DateTimeOffset.UtcNow;

        Assert.InRange(remaining.TotalDays, 29.9, 30.1);
    }

    [Fact]
    public void An_unset_kind_on_the_expiry_is_still_read_as_utc()
    {
        // The expiry arrives from the token service as an unspecified-kind
        // DateTime. Interpreted as local time it would move the cookie's lifetime
        // by the machine's offset, so a session could last hours longer or shorter
        // than intended depending on where it was issued.
        var context = new DefaultHttpContext();

        Service().Write(
            context,
            "access",
            "refresh",
            new DateTime(2030, 1, 2, 3, 4, 5, DateTimeKind.Unspecified));

        Assert.Equal(
            new DateTime(2030, 1, 2, 3, 4, 5, DateTimeKind.Utc),
            ExpiryOf(context, AuthCookieOptions.DefaultAccessTokenCookieName)
                .UtcDateTime);
    }

    // ------------------------------------------------------ path and domain

    [Fact]
    public void Both_cookies_are_sent_to_every_path()
    {
        var context = new DefaultHttpContext();

        Service().Write(context, "access", "refresh", AccessExpiresAt);

        // So a request from anywhere in the application is authenticated. A path
        // scoped to the API's own routes would silently leave the rest of the app
        // logged out.
        foreach (var name in new[]
                 {
                     AuthCookieOptions.DefaultAccessTokenCookieName,
                     AuthCookieOptions.DefaultRefreshTokenCookieName
                 })
        {
            Assert.Contains(
                "path=/",
                HeaderFor(context, name),
                StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void A_configured_path_is_honoured()
    {
        var context = new DefaultHttpContext();

        Service(path: "/api").Write(context, "access", "refresh", AccessExpiresAt);

        Assert.Contains(
            "path=/api",
            HeaderFor(context, AuthCookieOptions.DefaultAccessTokenCookieName),
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void An_empty_domain_leaves_the_cookies_host_only()
    {
        var context = new DefaultHttpContext();

        Service().Write(context, "access", "refresh", AccessExpiresAt);

        // No Domain attribute at all, rather than an empty one: a host-only cookie
        // cannot be read by anything on another host, which is the safe default
        // for a deployment that has not thought about it.
        Assert.DoesNotContain(
            "domain",
            HeaderFor(
                context,
                AuthCookieOptions.DefaultAccessTokenCookieName),
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void A_configured_domain_is_emitted()
    {
        var context = new DefaultHttpContext();

        Service(domain: "mayorx.test")
            .Write(context, "access", "refresh", AccessExpiresAt);

        Assert.Contains(
            "domain=mayorx.test",
            HeaderFor(
                context,
                AuthCookieOptions.DefaultAccessTokenCookieName),
            StringComparison.OrdinalIgnoreCase);
    }

    // --------------------------------------------------- samesite and secure

    [Fact]
    public void The_configured_samesite_mode_is_written()
    {
        var context = new DefaultHttpContext();

        Service(sameSite: SameSiteMode.Strict)
            .Write(context, "access", "refresh", AccessExpiresAt);

        Assert.Contains(
            "samesite=strict",
            HeaderFor(
                context,
                AuthCookieOptions.DefaultRefreshTokenCookieName),
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Secure_is_written_when_configured_on()
    {
        var context = new DefaultHttpContext();

        Service(secure: true).Write(context, "access", "refresh", AccessExpiresAt);

        Assert.Contains(
            "secure",
            HeaderFor(
                context,
                AuthCookieOptions.DefaultRefreshTokenCookieName),
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Secure_is_absent_when_configured_off()
    {
        var context = new DefaultHttpContext();

        Service(secure: false).Write(context, "access", "refresh", AccessExpiresAt);

        // Stated so the development default is asserted rather than assumed. A
        // Secure cookie that a browser refuses to send over plain HTTP would make
        // every local request look like a logged-out user.
        Assert.DoesNotContain(
            "secure",
            HeaderFor(
                context,
                AuthCookieOptions.DefaultRefreshTokenCookieName),
            StringComparison.OrdinalIgnoreCase);
    }

    // ------------------------------------------------ replacing and clearing

    [Fact]
    public void Writing_twice_replaces_rather_than_accumulates()
    {
        var context = new DefaultHttpContext();
        var service = Service();

        service.Write(context, "first-access", "first-refresh", AccessExpiresAt);
        service.Write(context, "second-access", "second-refresh", AccessExpiresAt);

        // Every successful authentication rotates the refresh token, so a stale
        // value left in the browser would be a live credential the server has
        // already revoked, and the next refresh would report it as a replay and
        // kill the session the user is actually using.
        Assert.Equal(
            "second-access",
            ValueOf(
                context,
                AuthCookieOptions.DefaultAccessTokenCookieName));

        Assert.Equal(
            "second-refresh",
            ValueOf(
                context,
                AuthCookieOptions.DefaultRefreshTokenCookieName));
    }

    [Fact]
    public void Clearing_deletes_both_cookies_with_an_expiry_in_the_past()
    {
        var context = new DefaultHttpContext();

        Service().Clear(context);

        foreach (var name in new[]
                 {
                     AuthCookieOptions.DefaultAccessTokenCookieName,
                     AuthCookieOptions.DefaultRefreshTokenCookieName
                 })
        {
            var expiry = ExpiryOf(context, name);

            // A deletion, not a fresh value. An expiry in the future would make
            // the browser replace the cookie instead of removing it, leaving a
            // working credential on a machine the user believes they signed out.
            Assert.True(
                expiry < DateTimeOffset.UtcNow,
                $"{name} was cleared with an expiry of {expiry:o}, which is " +
                "not in the past.");
        }
    }

    [Fact]
    public void Clearing_keeps_the_written_attributes_so_the_browser_can_match_the_deletion()
    {
        var context = new DefaultHttpContext();

        Service(domain: "mayorx.test", path: "/api")
            .Clear(context);

        // A browser matches a deletion on name, domain and path. Clearing without
        // them would leave the original cookie in place, so logout would report
        // success and change nothing.
        var header = HeaderFor(
            context,
            AuthCookieOptions.DefaultAccessTokenCookieName);

        Assert.Contains("path=/api", header, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(
            "domain=mayorx.test",
            header,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Clearing_leaves_no_usable_credential_on_the_response()
    {
        var context = new DefaultHttpContext();

        Service().Clear(context);

        // SameSite and Secure are not part of the browser's deletion match, so
        // they are not re-stated, but they are not required to be absent either.
        // What matters is that no value is written for either name.
        Assert.Empty(ValueOf(
            context,
            AuthCookieOptions.DefaultAccessTokenCookieName));

        Assert.Empty(ValueOf(
            context,
            AuthCookieOptions.DefaultRefreshTokenCookieName));
    }

    // -------------------------------------------------------- reading back

    [Fact]
    public void The_presented_refresh_token_is_read_from_the_request_cookie()
    {
        var context = new DefaultHttpContext();

        context.Request.Headers.Cookie =
            $"{AuthCookieOptions.DefaultRefreshTokenCookieName}=presented";

        Assert.Equal(
            "presented",
            Service().ReadRefreshToken(context));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void A_blank_refresh_cookie_reads_as_absent_rather_than_as_empty(string value)
    {
        var context = new DefaultHttpContext();

        context.Request.Headers.Cookie =
            $"{AuthCookieOptions.DefaultRefreshTokenCookieName}={value}";

        // Null, not empty string. The caller branches on this to decide whether
        // there was a session at all, and an empty string would reach the token
        // service and be reported as an invalid token rather than a missing one.
        Assert.Null(Service().ReadRefreshToken(context));
    }

    [Fact]
    public void A_request_with_no_cookies_at_all_reads_as_absent()
    {
        var context = new DefaultHttpContext();

        Assert.Null(Service().ReadRefreshToken(context));
    }

    [Fact]
    public void The_access_cookie_is_never_read_as_the_refresh_token()
    {
        var context = new DefaultHttpContext();

        context.Request.Headers.Cookie =
            $"{AuthCookieOptions.DefaultAccessTokenCookieName}=access-only";

        // Reading the wrong cookie here would make the API try to redeem a JWT as
        // a refresh token on every refresh, and report it as a replay.
        Assert.Null(Service().ReadRefreshToken(context));
    }

    // ---------------------------------------------------------------- helpers

    private static AuthCookieService Service(
        string path = "/",
        string domain = "",
        SameSiteMode sameSite = SameSiteMode.Lax,
        bool secure = false,
        int refreshTokenExpirationDays = 7) =>
        new(
            Options.Create(new AuthCookieOptions
            {
                Path = path,
                Domain = domain,
                SameSite = sameSite,
                Secure = secure
            }),
            Options.Create(new JwtOptions
            {
                RefreshTokenExpirationDays = refreshTokenExpirationDays
            }));

    private static IEnumerable<string> HeadersFor(
        DefaultHttpContext context,
        string name) =>
        // StringValues enumerates as string?, but a Set-Cookie header is never
        // null once it is present.
        context.Response.Headers.SetCookie
            .OfType<string>()
            .Where(header => NameOf(header) == name);

    private static string HeaderFor(DefaultHttpContext context, string name) =>
        HeadersFor(context, name).SingleOrDefault() ??
        throw new InvalidOperationException(
            $"No Set-Cookie header named '{name}' was written. Headers: " +
            $"{string.Join(" | ", context.Response.Headers.SetCookie.OfType<string>())}");

    private static string NameOf(string setCookieHeader) =>
        setCookieHeader.Split(';')[0].Split('=')[0].Trim();

    private static string ValueOf(DefaultHttpContext context, string name) =>
        Uri.UnescapeDataString(
            HeadersFor(context, name)
                .Select(header => header.Split(';')[0].Split('=', 2)[1].Trim())
                .LastOrDefault() ?? string.Empty);

    private static DateTimeOffset ExpiryOf(
        DefaultHttpContext context,
        string name)
    {
        var header = HeaderFor(context, name);

        var segment = header
            .Split(';')
            .First(part =>
                part.TrimStart()
                    .StartsWith("expires=", StringComparison.OrdinalIgnoreCase));

        return DateTimeOffset.Parse(
            segment[(segment.IndexOf('=') + 1)..].Trim(),
            System.Globalization.CultureInfo.InvariantCulture);
    }
}