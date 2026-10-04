using Api.Configuration;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace Api.Tests;

/// <summary>
/// The rules that decide whether a set of cookie settings is one the API should
/// accept at startup.
/// </summary>
/// <remarks>
/// These run once, when the host builds its options, and a wrong answer is not a
/// partial failure: a browser that rejects the cookie logs everybody out
/// silently, so the only place to catch it is before the process serves traffic.
/// That makes them startup validation rules rather than runtime behaviour, which
/// is exactly why they are unit tested here instead of being left as inline
/// predicates nobody can reach.
/// </remarks>
public sealed class AuthCookieOptionsTests
{
    // ------------------------------------------------------- startup defaults

    [Fact]
    public void The_defaults_serve_the_api_from_its_own_origin()
    {
        var options = new AuthCookieOptions();

        // Host-only, so the cookie cannot be read by anything on another host.
        Assert.Equal(string.Empty, options.Domain);

        // Every path, so a request from anywhere in the application is
        // authenticated rather than only the routes under a prefix.
        Assert.Equal("/", options.Path);

        // Lax rather than None: a web app on the same site as the API needs
        // nothing configured, and Lax still blocks cross-site POSTs.
        Assert.Equal(SameSiteMode.Lax, options.SameSite);

        Assert.Equal(
            AuthCookieOptions.DefaultAccessTokenCookieName,
            options.AccessTokenCookieName);

        Assert.Equal(
            AuthCookieOptions.DefaultRefreshTokenCookieName,
            options.RefreshTokenCookieName);
    }

    [Fact]
    public void The_two_cookies_have_different_names()
    {
        var options = new AuthCookieOptions();

        // Two cookies cannot share a name: the second would overwrite the first
        // in the browser's jar, and the API would read whichever was written last
        // and hand a refresh token to the bearer pipeline.
        Assert.NotEqual(
            options.AccessTokenCookieName,
            options.RefreshTokenCookieName);
    }

    // ------------------------------------------------------------- Secure rule

    [Fact]
    public void Secure_defaults_on_outside_development()
    {
        // The deployment case. A deployment that forgets to configure Secure is
        // exactly the one that would leak a session over plain HTTP, so the
        // default has to be the safe one.
        Assert.True(AuthCookieOptions.ResolveSecure(
            configured: null,
            isDevelopment: false));
    }

    [Fact]
    public void Secure_defaults_off_in_development()
    {
        // The local API is reached over plain HTTP, where a browser will not send
        // a Secure cookie back. Defaulting it on would make every local request
        // look like a logged-out user for a reason that has nothing to do with
        // the code under test.
        Assert.False(AuthCookieOptions.ResolveSecure(
            configured: null,
            isDevelopment: true));
    }

    [Theory]
    [InlineData(true, false, true)]
    [InlineData(true, true, true)]
    [InlineData(false, false, false)]
    [InlineData(false, true, false)]
    public void An_explicit_Secure_setting_wins_in_every_environment(
        bool configured,
        bool isDevelopment,
        bool expected)
    {
        // Configuration always wins over the derived default. The test host
        // depends on this to run over plain HTTP, and a cross-site deployment
        // depends on it to state the pairing explicitly.
        Assert.Equal(
            expected,
            AuthCookieOptions.ResolveSecure(configured, isDevelopment));
    }

    // -------------------------------------------------------- SameSite pairing

    [Theory]
    [InlineData(SameSiteMode.Strict, false)]
    [InlineData(SameSiteMode.Lax, false)]
    [InlineData(SameSiteMode.None, true)]
    public void SameSite_none_is_only_accepted_alongside_secure(
        SameSiteMode sameSite,
        bool secure)
    {
        var options = new AuthCookieOptions
        {
            SameSite = sameSite,
            Secure = secure
        };

        Assert.Equal(
            sameSite != SameSiteMode.None || secure,
            options.HasAcceptableSameSiteConfiguration());
    }

    [Fact]
    public void SameSite_none_without_secure_is_refused()
    {
        // Browsers discard such a cookie and say nothing, so the API would look
        // healthy in its own logs while every cross-site user stayed logged out.
        var options = new AuthCookieOptions
        {
            SameSite = SameSiteMode.None,
            Secure = false
        };

        Assert.False(options.HasAcceptableSameSiteConfiguration());
    }

    [Fact]
    public void The_refusal_is_about_the_pairing_and_not_about_secure_on_its_own()
    {
        // The supported cross-site deployment must still start. Refusing this
        // would leave weakening the cookie as the only way to put a web app on a
        // different site to the API.
        var options = new AuthCookieOptions
        {
            SameSite = SameSiteMode.None,
            Secure = true
        };

        Assert.True(options.HasAcceptableSameSiteConfiguration());
    }

    [Fact]
    public void SameSite_none_over_plain_http_is_refused_even_in_development()
    {
        // Development is why Secure defaults off, not why the combination becomes
        // acceptable. A developer who configures None gets a cookie their own
        // browser will discard, so it is refused here too.
        var options = new AuthCookieOptions
        {
            SameSite = SameSiteMode.None,
            Secure = false
        };

        Assert.False(options.HasAcceptableSameSiteConfiguration());
    }

    // ----------------------------------------------------------- cookie names

    [Fact]
    public void Both_names_are_required()
    {
        var options = new AuthCookieOptions();

        Assert.True(options.HasRequiredCookieNames());
    }

    [Theory]
    [InlineData("", "mayorx.rt")]
    [InlineData("   ", "mayorx.rt")]
    [InlineData("mayorx.at", "")]
    [InlineData("mayorx.at", "   ")]
    [InlineData("", "")]
    public void A_blank_name_is_refused(string access, string refresh)
    {
        // A blank name produces a cookie the API cannot read back, so it would
        // authenticate nobody while looking perfectly healthy.
        var options = new AuthCookieOptions
        {
            AccessTokenCookieName = access,
            RefreshTokenCookieName = refresh
        };

        Assert.False(options.HasRequiredCookieNames());
    }

    [Fact]
    public void Renaming_the_cookies_is_allowed()
    {
        // A deployment may need its own names, and that must not trip validation.
        var options = new AuthCookieOptions
        {
            AccessTokenCookieName = "custom.at",
            RefreshTokenCookieName = "custom.rt"
        };

        Assert.True(options.HasRequiredCookieNames());
    }

    // ------------------------------------------------------ OpenAPI agreement

    [Fact]
    public void The_openapi_scheme_name_is_distinct_from_the_cookie_names()
    {
        // The scheme name is how the requirement addresses the definition in the
        // document. It is a label, not a header or cookie name, so reusing a
        // cookie name for it would make the two drift into each other.
        Assert.Equal("SessionCookie", AuthCookieOptions.OpenApiSecuritySchemeName);

        Assert.NotEqual(
            AuthCookieOptions.OpenApiSecuritySchemeName,
            AuthCookieOptions.DefaultAccessTokenCookieName);

        Assert.NotEqual(
            AuthCookieOptions.OpenApiSecuritySchemeName,
            AuthCookieOptions.DefaultRefreshTokenCookieName);
    }

    [Fact]
    public void The_options_section_name_is_the_one_configuration_uses()
    {
        // Bound by name in the composition root, and documented under this name
        // for cross-site deployments. Changing one without the other would leave
        // the documented override silently ignored.
        Assert.Equal("AuthCookies", AuthCookieOptions.SectionName);
    }
}