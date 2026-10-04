using System.Net;
using Api.Configuration;
using Xunit;

namespace Api.IntegrationTests;

/// <summary>
/// The browser session contract: where the credentials live, and what the
/// browser is told about them.
/// </summary>
/// <remarks>
/// The properties here are the reason the cookie exists. If a refresh token can
/// be read by page script it can be stolen by any script that runs on the page,
/// so "the response body does not contain it" is the security property itself,
/// and it is asserted directly rather than inferred from the fact that a cookie
/// was set.
/// </remarks>
public sealed class AuthCookieTests : IAsyncLifetime
{
    private readonly MayorxApiFactory _factory = MayorxApiFactory.WithSettings(
        new Dictionary<string, string?>
        {
            ["RateLimiting:AuthenticationPermitLimit"] = "0"
        });

    private BrowserClient _browser = null!;
    private HttpClient _controlled = null!;

    public async Task InitializeAsync()
    {
        _factory.Initialise();
        await _factory.MigrateAsync();

        _browser = new BrowserClient(_factory);
        _controlled = _factory.CreateCookieControlledClient();
    }

    public Task DisposeAsync()
    {
        _browser?.Dispose();
        _controlled?.Dispose();
        _factory.Dispose();

        MayorxApiFactory.ResetAuthCookieEnvironment();

        return Task.CompletedTask;
    }

    [Fact]
    public async Task Register_sets_both_session_cookies_as_http_only()
    {
        using var response = await RegisterAsync(_browser, "cookie-httponly");

        TestHttp.EnsureSucceeded(response);

        foreach (var name in SessionCookieNames)
        {
            var header = TestHttp.SetCookieFor(response, name);

            Assert.Contains("httponly", header, StringComparison.OrdinalIgnoreCase);

            // Path=/ so a request from anywhere in the app is authenticated,
            // rather than only from the routes that happen to match a prefix.
            Assert.Contains("path=/", header, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public async Task No_token_appears_anywhere_in_the_response_body()
    {
        using var response = await RegisterAsync(_browser, "cookie-body");

        var raw = await response.Content.ReadAsStringAsync();

        Assert.DoesNotContain("accessToken", raw, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("refreshToken", raw, StringComparison.OrdinalIgnoreCase);

        // And neither credential that was issued is echoed anywhere in it. This
        // is the property the whole design exists to provide: a token in a
        // response body is a token page script can read.
        Assert.DoesNotContain(_browser.AccessToken, raw, StringComparison.Ordinal);
        Assert.DoesNotContain(_browser.RefreshToken, raw, StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_refresh_cookie_outlives_the_access_cookie()
    {
        using var response = await RegisterAsync(_browser, "cookie-lifetimes");

        var access = TestHttp.SetCookieFor(
            response,
            AuthCookieOptions.DefaultAccessTokenCookieName);
        var refresh = TestHttp.SetCookieFor(
            response,
            AuthCookieOptions.DefaultRefreshTokenCookieName);

        // The whole point of the pair: a short-lived access credential is renewed
        // with a long-lived one that the browser holds without page script being
        // able to see it. A refresh token that expired with the access token
        // would make the browser demand a password every hour.
        Assert.True(
            TestHttp.CookieExpiryOf(refresh) > TestHttp.CookieExpiryOf(access),
            "The session cannot renew itself if the refresh cookie expires " +
            "with the access cookie.");
    }

    [Fact]
    public async Task Logging_in_sets_the_same_cookies_as_registering_does()
    {
        var session = await TestSession.RegisterAsync(_browser, "cookie-login");

        using var response = await _browser.PostAsync(
            "api/v1/auth/login",
            new
            {
                email = session.Email,
                password = "Integration-Test-Password-42"
            });

        TestHttp.EnsureSucceeded(response);

        foreach (var name in SessionCookieNames)
        {
            Assert.Contains(
                "httponly",
                TestHttp.SetCookieFor(response, name),
                StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public async Task The_access_cookie_alone_authenticates_a_protected_route()
    {
        var session = await TestSession.RegisterAsync(_browser, "cookie-access");

        // Carrying the access cookie and nothing else, so nothing that still
        // works because of a bearer header can be mistaken for the cookie doing
        // its job.
        using var controlled = _factory.CreateCookieControlledClient();
        using var me = await controlled.SendAsync(
            new HttpRequestMessage(HttpMethod.Get, "api/v1/users/me")
                .WithCookie(
                    AuthCookieOptions.DefaultAccessTokenCookieName,
                    session.AccessToken));

        Assert.Equal(HttpStatusCode.OK, me.StatusCode);
    }

    [Fact]
    public async Task A_browser_with_no_cookies_is_refused()
    {
        using var anonymous = new BrowserClient(_factory);

        using var response = await anonymous.GetAsync("api/v1/workspaces");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Logout_deletes_both_cookies()
    {
        await TestSession.RegisterAsync(_browser, "cookie-logout");

        using var response = await _browser.PostAsync("api/v1/auth/logout");

        TestHttp.EnsureSucceeded(response);

        foreach (var name in SessionCookieNames)
        {
            var header = TestHttp.SetCookieFor(response, name);

            // A deletion, not a fresh value. The expiry has to be in the past or
            // the browser replaces the cookie rather than removing it, which
            // would leave a working credential on a machine the user believes
            // they just signed out of.
            Assert.True(
                TestHttp.CookieExpiryOf(header) < DateTimeOffset.UtcNow,
                $"{name} was deleted with an expiry of " +
                $"{TestHttp.CookieExpiryOf(header):o}, which is not in the past.");
        }

        // And the browser really did drop them.
        Assert.Empty(_browser.Cookies);

        using var after = await _browser.GetAsync("api/v1/workspaces");

        Assert.Equal(HttpStatusCode.Unauthorized, after.StatusCode);
    }

    [Fact]
    public async Task A_failed_refresh_clears_the_stale_cookie()
    {
        var session = await TestSession.RegisterAsync(_browser, "cookie-stale");

        // Rotate once, then present the token rotation already consumed. Without
        // clearing, the cookie would still hold a dead credential and every
        // automatic refresh afterwards would keep presenting it.
        using var rotated = await RefreshAsync(session.RefreshToken);
        TestHttp.EnsureSucceeded(rotated);

        using var replay = await RefreshAsync(session.RefreshToken);

        Assert.Equal(HttpStatusCode.Unauthorized, replay.StatusCode);

        Assert.True(
            TestHttp.CookieExpiryOf(TestHttp.SetCookieFor(
                replay,
                AuthCookieOptions.DefaultRefreshTokenCookieName))
                < DateTimeOffset.UtcNow,
            "A rejected refresh must remove the credential from the browser, " +
            "not leave the dead one in place.");
    }

    [Fact]
    public async Task Cookies_are_marked_secure_by_default_outside_development()
    {
        using var host = StartHost(sameSite: null, secure: null);

        try
        {
            await host.MigrateAsync();

            using var browser = new BrowserClient(host);

            using var response = await RegisterAsync(browser, "cookie-secure");

            TestHttp.EnsureSucceeded(response);

            // The API's own default has to be Secure, because a deployment that
            // forgets to configure it is exactly the one that would leak a
            // session over plain HTTP.
            Assert.Contains(
                "secure",
                TestHttp.SetCookieFor(
                    response,
                    AuthCookieOptions.DefaultRefreshTokenCookieName),
                StringComparison.OrdinalIgnoreCase);

            // And Lax rather than None, so a web app served from the same site
            // as the API needs nothing configured at all.
            Assert.Contains(
                "samesite=lax",
                TestHttp.SetCookieFor(
                    response,
                    AuthCookieOptions.DefaultRefreshTokenCookieName),
                StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            MayorxApiFactory.ResetAuthCookieEnvironment();
        }
    }

    [Fact]
    public async Task SameSite_none_without_secure_refuses_to_start()
    {
        // Browsers silently discard such a cookie, so the API would appear to
        // work in its own logs while every real user stayed logged out. Failing
        // at startup names the problem while it can still be fixed.
        using var host = StartHost(sameSite: "None", secure: "false");

        try
        {
            // Both steps are inside the recording. WebApplicationFactory starts
            // the host the first time a client is created, so the startup
            // validation this test is about throws there rather than on a
            // request. Leaving CreateClient outside would let the exception
            // escape the assertion and report as an unhandled error instead of
            // the expected refusal.
            var failure = await Record.ExceptionAsync(async () =>
            {
                using var client = host.CreateClient();

                await client.GetAsync("api/v1/health/live");
            });

            Assert.NotNull(failure);
            Assert.Contains(
                "SameSite=None",
                FlattenMessages(failure),
                StringComparison.Ordinal);
        }
        finally
        {
            MayorxApiFactory.ResetAuthCookieEnvironment();
        }
    }

    [Fact]
    public async Task SameSite_none_with_secure_starts()
    {
        // The configuration a cross-site deployment needs. If this failed to
        // start, the only way to deploy a web app on a different site than the
        // API would be to weaken the cookie instead.
        using var host = StartHost(sameSite: "None", secure: "true");

        try
        {
            using var client = host.CreateClient();

            using var response = await client.GetAsync("api/v1/health/live");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
        finally
        {
            MayorxApiFactory.ResetAuthCookieEnvironment();
        }
    }

    // ---------------------------------------------------------------- helpers

    private static string[] SessionCookieNames =>
    [
        AuthCookieOptions.DefaultAccessTokenCookieName,
        AuthCookieOptions.DefaultRefreshTokenCookieName
    ];

    /// <summary>
    /// A host configured with exactly these cookie settings, on the API's own
    /// defaults for anything left null.
    /// </summary>
    /// <remarks>
    /// Its own host rather than the shared one, because the shared host was built
    /// during setup and reads its configuration once. Changing a setting after
    /// that would change nothing, which is the kind of test that passes for the
    /// wrong reason. The caller restores the process-wide settings once this
    /// host has read them.
    /// </remarks>
    private static MayorxApiFactory StartHost(string? sameSite, string? secure)
    {
        var host = MayorxApiFactory.WithSettings(
            new Dictionary<string, string?>
            {
                ["RateLimiting:AuthenticationPermitLimit"] = "0"
            });

        host.OwnsAuthCookieSettings();
        host.ConfigureAuthCookies(sameSite, secure);
        host.Initialise();

        return host;
    }

    private static Task<HttpResponseMessage> RegisterAsync(
        BrowserClient browser,
        string suffix) =>
        browser.PostAsync(
            "api/v1/auth/register",
            new
            {
                email = $"{suffix}@example.test",
                password = "Integration-Test-Password-42",
                firstName = "Integration",
                lastName = "Owner",
                workspaceName = $"Workspace {suffix}"
            });

    private Task<HttpResponseMessage> RefreshAsync(string refreshToken) =>
        _controlled.SendAsync(
            new HttpRequestMessage(HttpMethod.Post, "api/v1/auth/refresh")
                .WithCookie(
                    AuthCookieOptions.DefaultRefreshTokenCookieName,
                    refreshToken));

    private static string FlattenMessages(Exception exception)
    {
        var messages = new List<string>();

        for (var current = exception;
             current is not null;
             current = current.InnerException)
        {
            messages.Add(current.Message);
        }

        return string.Join(" | ", messages);
    }
}