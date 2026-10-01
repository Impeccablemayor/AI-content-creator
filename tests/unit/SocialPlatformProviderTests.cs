using System.Net;
using System.Text.Json;
using Api.Application.SocialAccounts;
using Api.Configuration;
using Api.Domain.Enums;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Primitives;
using Xunit;

namespace Api.Tests;

public sealed class SocialPlatformProviderTests
{
    private const string AccessToken = "provider-access-token";
    private const string RefreshToken = "provider-refresh-token";

    [Fact]
    public void A_fully_configured_platform_reports_itself_configured()
    {
        Assert.True(CreateLinkedInProvider().IsConfigured);
        Assert.True(CreateXProvider().IsConfigured);
    }

    [Theory]
    [InlineData("ClientSecret")]
    [InlineData("ClientId")]
    [InlineData("RedirectUri")]
    public void A_partially_configured_platform_is_not_usable(string missing)
    {
        var options = CreateLinkedInOptions();
        switch (missing)
        {
            case "ClientSecret":
                options.ClientSecret = string.Empty;
                break;
            case "ClientId":
                options.ClientId = string.Empty;
                break;
            default:
                options.RedirectUri = string.Empty;
                break;
        }

        var provider = new LinkedInSocialPlatformProvider(
            new HttpClient(new StubHttpMessageHandler((_, _) => Task.FromResult(new HttpResponseMessage()))),
            options);

        Assert.False(provider.IsConfigured);
    }

    [Fact]
    public void A_platform_without_scopes_is_not_usable()
    {
        var options = CreateLinkedInOptions();
        options.Scopes = [];

        var provider = new LinkedInSocialPlatformProvider(
            new HttpClient(new StubHttpMessageHandler((_, _) => Task.FromResult(new HttpResponseMessage()))),
            options);

        Assert.False(provider.IsConfigured);
    }

    [Fact]
    public void The_authorization_uri_carries_the_oauth_authorization_code_parameters()
    {
        var provider = CreateLinkedInProvider();

        var uri = provider.BuildAuthorizationUri("state-value", codeChallenge: null);
        var query = ParseQuery(uri);

        Assert.Equal("code", query["response_type"]);
        Assert.Equal("linkedin-client", query["client_id"]);
        Assert.Equal("https://app.example.com/api/v1/social/linkedin/callback", query["redirect_uri"]);
        Assert.Equal("state-value", query["state"]);
        Assert.Contains("openid", query["scope"].ToString(), StringComparison.Ordinal);
    }

    /// <summary>
    /// The redirect URI must come from configuration. Accepting it from the
    /// request would let a caller redirect credentials to their own host.
    /// </summary>
    [Fact]
    public void The_authorization_uri_only_uses_the_configured_redirect_uri()
    {
        var options = CreateLinkedInOptions();
        var provider = new LinkedInSocialPlatformProvider(
            new HttpClient(new StubHttpMessageHandler((_, _) => Task.FromResult(new HttpResponseMessage()))),
            options);

        var uri = provider.BuildAuthorizationUri("state-value", codeChallenge: null);

        Assert.Equal(options.RedirectUri, ParseQuery(uri)["redirect_uri"]);
    }

    [Fact]
    public void The_authorization_uri_omits_pkce_when_no_challenge_is_supplied()
    {
        var uri = CreateXProvider().BuildAuthorizationUri("state-value", codeChallenge: null);
        var query = ParseQuery(uri);

        Assert.False(query.ContainsKey("code_challenge"));
        Assert.False(query.ContainsKey("code_challenge_method"));
    }

    [Fact]
    public void The_authorization_uri_requests_an_s256_pkce_challenge_when_one_is_supplied()
    {
        var uri = CreateXProvider().BuildAuthorizationUri("state-value", "challenge-value");
        var query = ParseQuery(uri);

        Assert.Equal("challenge-value", query["code_challenge"]);
        Assert.Equal("S256", query["code_challenge_method"]);
    }

    /// <summary>
    /// RFC 7636 appendix B test vector.
    /// </summary>
    [Fact]
    public void The_pkce_challenge_matches_the_rfc_7636_test_vector()
    {
        var challenge = SocialPkce.CreateCodeChallenge(
            "dBjftJeZ4CVP-mB92K27uhbUJU1p1r_wW1gFWFOEjXk");

        Assert.Equal("E9Melhoa2OwvFrEMTJguCHaoeK1t8URWbuGJSstw-cM", challenge);
    }

    [Fact]
    public void The_pkce_verifier_is_random_and_url_safe()
    {
        var first = SocialPkce.CreateCodeVerifier();
        var second = SocialPkce.CreateCodeVerifier();

        Assert.NotEqual(first, second);
        Assert.DoesNotContain('+', first);
        Assert.DoesNotContain('/', first);
        Assert.DoesNotContain('=', first);
    }

    [Fact]
    public async Task A_token_exchange_posts_the_authorization_code_grant()
    {
        Dictionary<string, string>? sentFields = null;
        var provider = CreateLinkedInProvider(async (request, _) =>
        {
            sentFields = await ReadFormAsync(request);

            return JsonResponse("""
                {"access_token":"provider-access-token","expires_in":3600,"scope":"openid profile"}
                """);
        });

        var result = await provider.ExchangeAuthorizationCodeAsync("auth-code", null, CancellationToken.None);

        Assert.Equal(AccessToken, result.AccessToken);
        Assert.Equal("authorization_code", sentFields!["grant_type"]);
        Assert.Equal("auth-code", sentFields["code"]);
        Assert.Equal("linkedin-client", sentFields["client_id"]);
        Assert.Equal("linkedin-secret", sentFields["client_secret"]);
        Assert.Equal("https://app.example.com/api/v1/social/linkedin/callback", sentFields["redirect_uri"]);
        Assert.True(result.ExpiresAt > DateTime.UtcNow.AddMinutes(50));
        Assert.Equal(["openid", "profile"], result.Scopes);
    }

    [Fact]
    public async Task A_token_exchange_returns_the_refresh_token_when_the_platform_issues_one()
    {
        var provider = CreateXProvider((_, _) => Task.FromResult(JsonResponse("""
            {"access_token":"provider-access-token","refresh_token":"provider-refresh-token","expires_in":5184000}
            """)));

        var result = await provider.ExchangeAuthorizationCodeAsync(
            "auth-code",
            "verifier",
            CancellationToken.None);

        Assert.Equal(RefreshToken, result.RefreshToken);
    }

    [Fact]
    public async Task A_token_exchange_for_a_platform_without_refresh_tokens_returns_none()
    {
        var provider = CreateLinkedInProvider((_, _) => Task.FromResult(JsonResponse("""
            {"access_token":"provider-access-token","expires_in":3600}
            """)));

        var result = await provider.ExchangeAuthorizationCodeAsync(
            "auth-code",
            null,
            CancellationToken.None);

        Assert.Null(result.RefreshToken);
    }

    [Fact]
    public async Task A_token_exchange_sends_the_pkce_verifier_when_provided()
    {
        Dictionary<string, string>? sentFields = null;
        var provider = CreateXProvider(async (request, _) =>
        {
            sentFields = await ReadFormAsync(request);

            return JsonResponse("""{"access_token":"provider-access-token"}""");
        });

        await provider.ExchangeAuthorizationCodeAsync("auth-code", "the-verifier", CancellationToken.None);

        Assert.Equal("the-verifier", sentFields!["code_verifier"]);
    }

    [Fact]
    public async Task A_rejected_token_exchange_maps_to_an_authentication_failure()
    {
        var provider = CreateLinkedInProvider((_, _) => Task.FromResult(
            new HttpResponseMessage(HttpStatusCode.Unauthorized)
            {
                Content = new StringContent("invalid_client")
            }));

        var exception = await Assert.ThrowsAsync<SocialProviderAuthenticationException>(
            () => provider.ExchangeAuthorizationCodeAsync("auth-code", null, CancellationToken.None));

        Assert.Contains("rejected the configured credentials", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_server_error_during_token_exchange_maps_to_a_request_failure()
    {
        var provider = CreateLinkedInProvider((_, _) => Task.FromResult(
            new HttpResponseMessage(HttpStatusCode.InternalServerError)));

        var exception = await Assert.ThrowsAsync<SocialProviderRequestException>(
            () => provider.ExchangeAuthorizationCodeAsync("auth-code", null, CancellationToken.None));

        Assert.Contains("500", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// A token endpoint body can contain credentials. Failures must not carry
    /// the body into an exception, because exceptions are logged.
    /// </summary>
    [Fact]
    public async Task A_failed_token_exchange_never_surfaces_the_response_body()
    {
        const string leakedToken = "leaked-secret-token-in-body";
        var provider = CreateLinkedInProvider((_, _) => Task.FromResult(
            new HttpResponseMessage(HttpStatusCode.BadRequest)
            {
                Content = new StringContent($"{{\"error\":\"invalid_grant\",\"access_token\":\"{leakedToken}\"}}")
            }));

        var exception = await Assert.ThrowsAsync<SocialProviderRequestException>(
            () => provider.ExchangeAuthorizationCodeAsync("auth-code", null, CancellationToken.None));

        Assert.DoesNotContain(leakedToken, exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_token_response_without_an_access_token_is_rejected()
    {
        var provider = CreateLinkedInProvider((_, _) => Task.FromResult(
            JsonResponse("""{"token_type":"Bearer"}""")));

        await Assert.ThrowsAsync<SocialProviderRequestException>(
            () => provider.ExchangeAuthorizationCodeAsync("auth-code", null, CancellationToken.None));
    }

    [Fact]
    public async Task A_linkedin_profile_is_read_from_the_userinfo_response()
    {
        var provider = CreateLinkedInProvider((_, _) => Task.FromResult(JsonResponse("""
            {"sub":"linkedin-user-1","name":"Mayorx Tech","picture":"https://media.example/pic.png"}
            """)));

        var profile = await provider.GetProfileAsync(AccessToken, CancellationToken.None);

        Assert.Equal("linkedin-user-1", profile.PlatformAccountId);
        Assert.Equal("Mayorx Tech", profile.DisplayName);
        Assert.Equal("https://media.example/pic.png", profile.ProfileUrl);
    }

    [Fact]
    public async Task A_linkedin_profile_without_a_subject_is_rejected()
    {
        var provider = CreateLinkedInProvider((_, _) => Task.FromResult(JsonResponse("""{"name":"No Id"}""")));

        await Assert.ThrowsAsync<SocialProviderRequestException>(
            () => provider.GetProfileAsync(AccessToken, CancellationToken.None));
    }

    [Fact]
    public async Task An_x_profile_is_read_from_the_users_me_response()
    {
        var provider = CreateXProvider((_, _) => Task.FromResult(JsonResponse("""
            {"data":{"id":"x-user-1","name":"Mayorx","username":"mayorx"}}
            """)));

        var profile = await provider.GetProfileAsync(AccessToken, CancellationToken.None);

        Assert.Equal("x-user-1", profile.PlatformAccountId);
        Assert.Equal("mayorx", profile.Username);
        Assert.Equal("Mayorx", profile.DisplayName);
        Assert.Equal("https://x.com/mayorx", profile.ProfileUrl);
    }

    [Fact]
    public async Task An_x_profile_request_asks_for_the_fields_the_mapping_needs()
    {
        Uri? requested = null;
        var provider = CreateXProvider((request, _) =>
        {
            requested = request.RequestUri;

            return Task.FromResult(JsonResponse("""
                {"data":{"id":"x-user-1","name":"Mayorx","username":"mayorx"}}
                """));
        });

        await provider.GetProfileAsync(AccessToken, CancellationToken.None);

        Assert.Contains("user.fields=username", requested!.Query, StringComparison.Ordinal);
    }

    [Fact]
    public async Task An_x_profile_without_data_is_rejected()
    {
        var provider = CreateXProvider((_, _) => Task.FromResult(JsonResponse("""{"errors":[]}""")));

        await Assert.ThrowsAsync<SocialProviderRequestException>(
            () => provider.GetProfileAsync(AccessToken, CancellationToken.None));
    }

    [Fact]
    public async Task A_profile_request_uses_bearer_authentication()
    {
        string? scheme = null;
        string? parameter = null;
        var provider = CreateLinkedInProvider((request, _) =>
        {
            scheme = request.Headers.Authorization?.Scheme;
            parameter = request.Headers.Authorization?.Parameter;

            return Task.FromResult(JsonResponse("""{"sub":"linkedin-user-1"}"""));
        });

        await provider.GetProfileAsync(AccessToken, CancellationToken.None);

        Assert.Equal("Bearer", scheme);
        Assert.Equal(AccessToken, parameter);
    }

    [Fact]
    public async Task A_rejected_profile_request_maps_to_an_authentication_failure()
    {
        var provider = CreateLinkedInProvider((_, _) => Task.FromResult(
            new HttpResponseMessage(HttpStatusCode.Forbidden)));

        await Assert.ThrowsAsync<SocialProviderAuthenticationException>(
            () => provider.GetProfileAsync(AccessToken, CancellationToken.None));
    }

    [Fact]
    public void The_resolver_returns_the_provider_for_a_supported_platform()
    {
        var resolver = new SocialPlatformProviderResolver(
        [
            CreateLinkedInProvider(),
            CreateXProvider()
        ]);

        Assert.Equal(ContentPlatform.LinkedIn, resolver.Get(ContentPlatform.LinkedIn).Platform);
        Assert.Equal(ContentPlatform.X, resolver.Get(ContentPlatform.X).Platform);
    }

    [Theory]
    [InlineData("linkedin", true, ContentPlatform.LinkedIn)]
    [InlineData("LinkedIn", true, ContentPlatform.LinkedIn)]
    [InlineData("x", true, ContentPlatform.X)]
    [InlineData("X", true, ContentPlatform.X)]
    [InlineData("  linkedin  ", true, ContentPlatform.LinkedIn)]
    [InlineData("facebook", false, null)]
    [InlineData("instagram", false, null)]
    [InlineData("", false, null)]
    [InlineData(null, false, null)]
    public void Only_linkedin_and_x_parse_as_supported_platforms(
        string? value,
        bool expected,
        ContentPlatform? expectedPlatform)
    {
        var parsed = SocialPlatformParser.TryParse(value, out var platform);

        Assert.Equal(expected, parsed);
        if (expected)
        {
            Assert.Equal(expectedPlatform, platform);
        }
    }

    private static LinkedInOptions CreateLinkedInOptions() => new()
    {
        ClientId = "linkedin-client",
        ClientSecret = "linkedin-secret",
        RedirectUri = "https://app.example.com/api/v1/social/linkedin/callback",
        Scopes = ["openid", "profile"]
    };

    private static XOptions CreateXOptions() => new()
    {
        ClientId = "x-client",
        ClientSecret = "x-secret",
        RedirectUri = "https://app.example.com/api/v1/social/x/callback",
        Scopes = ["tweet.read", "users.read", "offline.access"]
    };

    private static LinkedInSocialPlatformProvider CreateLinkedInProvider(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>>? handler = null) =>
        new(
            new HttpClient(new StubHttpMessageHandler(
                handler ?? ((_, _) => Task.FromResult(JsonResponse("""{"access_token":"x"}"""))))),
            CreateLinkedInOptions());

    private static XSocialPlatformProvider CreateXProvider(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>>? handler = null) =>
        new(
            new HttpClient(new StubHttpMessageHandler(
                handler ?? ((_, _) => Task.FromResult(JsonResponse("""{"access_token":"x"}"""))))),
            CreateXOptions());

    private static HttpResponseMessage JsonResponse(string json) =>
        new(HttpStatusCode.OK)
        {
            Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
        };

    private static Dictionary<string, string> ParseQuery(Uri uri) =>
        QueryHelpers.ParseQuery(uri.Query)
            .ToDictionary(
                entry => entry.Key,
                entry => entry.Value.ToString(),
                StringComparer.Ordinal);

    private static async Task<Dictionary<string, string>> ReadFormAsync(
        HttpRequestMessage request)
    {
        var body = await request.Content!.ReadAsStringAsync();
        var parsed = QueryHelpers.ParseQuery(body);
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var entry in parsed)
        {
            result[entry.Key] = entry.Value.ToString();
        }

        return result;
    }

    private sealed class StubHttpMessageHandler(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            send(request, cancellationToken);
    }
}
