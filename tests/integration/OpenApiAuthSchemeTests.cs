using System.Net;
using System.Text.Json;
using Api.Configuration;
using Xunit;

namespace Api.IntegrationTests;

/// <summary>
/// Whether the generated API documentation describes how the API actually
/// authenticates.
/// </summary>
/// <remarks>
/// <para>
/// This is not a cosmetic concern. The OpenAPI document is what generates client
/// code. A document that advertises only a bearer header tells every generated
/// browser client to take the access token, hold it in JavaScript, and attach it
/// by hand, which is precisely the contract the httpOnly session cookie exists
/// to remove. The documentation is how that mistake gets made.
/// </para>
/// <para>
/// The bearer header is still advertised, because it is still accepted and
/// non-browser clients still need it. What is asserted here is that both are
/// offered as alternatives. OpenAPI reads several entries in a security
/// requirement as OR, and several schemes inside a single entry as AND, so the
/// two are told apart by structure rather than by intent.
/// </para>
/// </remarks>
public sealed class OpenApiAuthSchemeTests
{
    private const string DocumentPath = "swagger/v1/swagger.json";

    [Fact]
    public async Task The_document_declares_the_session_cookie_scheme()
    {
        using var factory = await StartAsync();

        using var document = await ReadDocumentAsync(factory);

        var schemes = document.RootElement
            .GetProperty("components")
            .GetProperty("securitySchemes");

        var cookie = schemes.GetProperty(
            AuthCookieOptions.OpenApiSecuritySchemeName);

        // Named after the cookie the API actually sets, and typed as a cookie. A
        // scheme that claimed the header would send a generated client looking
        // for something the API never reads.
        Assert.Equal(
            AuthCookieOptions.DefaultAccessTokenCookieName,
            cookie.GetProperty("name").GetString());

        Assert.Equal("apiKey", cookie.GetProperty("type").GetString());
        Assert.Equal("cookie", cookie.GetProperty("in").GetString());
    }

    [Fact]
    public async Task The_bearer_scheme_is_still_declared()
    {
        using var factory = await StartAsync();

        using var document = await ReadDocumentAsync(factory);

        var schemes = document.RootElement
            .GetProperty("components")
            .GetProperty("securitySchemes");

        Assert.Equal(
            "http",
            schemes.GetProperty("Bearer").GetProperty("type").GetString());
        Assert.Equal(
            "bearer",
            schemes.GetProperty("Bearer").GetProperty("scheme").GetString());
    }

    [Fact]
    public async Task A_protected_operation_offers_both_credentials_as_alternatives()
    {
        using var factory = await StartAsync();

        using var document = await ReadDocumentAsync(factory);

        var schemes = RequirementSchemeNames(document, "/api/v1/workspaces", "get");

        Assert.Contains(
            AuthCookieOptions.OpenApiSecuritySchemeName,
            schemes);

        Assert.Contains("Bearer", schemes);

        // Two separate entries, each naming one scheme. Had both been placed in a
        // single entry, OpenAPI would read the requirement as "send a cookie and
        // send a bearer header", and a generated client would send both on every
        // request. Asserted structurally because that mistake produces a document
        // that still mentions both schemes and still looks correct.
        Assert.Equal(2, RequirementEntries(document, "/api/v1/workspaces", "get").Count);

        Assert.All(
            RequirementEntries(document, "/api/v1/workspaces", "get"),
            entry => Assert.Single(entry));
    }

    [Fact]
    public async Task The_cookie_is_listed_before_the_header()
    {
        using var factory = await StartAsync();

        using var document = await ReadDocumentAsync(factory);

        var schemes = RequirementSchemeNames(document, "/api/v1/workspaces", "get");

        // The cookie is what a browser uses, so it is the first thing a reader
        // of the generated docs should encounter.
        Assert.Equal(
            AuthCookieOptions.OpenApiSecuritySchemeName,
            schemes[0]);
    }

    [Fact]
    public async Task An_anonymous_operation_declares_no_credential()
    {
        using var factory = await StartAsync();

        using var document = await ReadDocumentAsync(factory);

        // Login and register are how a caller obtains a session. Advertising a
        // credential requirement on them would tell a generated client to
        // authenticate before it has anything to authenticate with.
        foreach (var path in new[]
                 {
                     "/api/v1/auth/login",
                     "/api/v1/auth/register"
                 })
        {
            var operation = document.RootElement
                .GetProperty("paths")
                .GetProperty(path)
                .GetProperty("post");

            Assert.True(
                !operation.TryGetProperty("security", out _),
                $"{path} is how a session is obtained and must not declare a " +
                "security requirement.");
        }
    }

    [Fact]
    public async Task The_documented_cookie_matches_the_one_the_api_sets()
    {
        using var factory = await StartAsync();

        using var document = await ReadDocumentAsync(factory);

        var schemes = document.RootElement
            .GetProperty("components")
            .GetProperty("securitySchemes");

        // The scheme name is declared from the options type rather than repeated
        // as a literal, so this is a guard against the two drifting apart in a
        // way that leaves a generated client authenticating with a cookie the API
        // never reads.
        Assert.Equal(
            AuthCookieOptions.DefaultAccessTokenCookieName,
            schemes.GetProperty(
                    AuthCookieOptions.OpenApiSecuritySchemeName)
                .GetProperty("name")
                .GetString());
    }

    // ---------------------------------------------------------------- helpers

    private static List<HashSet<string>> RequirementEntries(
        JsonDocument document,
        string path,
        string method) =>
        Operation(document, path, method)
            .GetProperty("security")
            .EnumerateArray()
            .Select(entry =>
                entry.EnumerateObject()
                    .Select(property => property.Name)
                    .ToHashSet(StringComparer.Ordinal))
            .ToList();

    private static List<string> RequirementSchemeNames(
        JsonDocument document,
        string path,
        string method) =>
        Operation(document, path, method)
            .GetProperty("security")
            .EnumerateArray()
            .SelectMany(entry =>
                entry.EnumerateObject()
                    .Select(property => property.Name))
            .ToList();

    private static JsonElement Operation(
        JsonDocument document,
        string path,
        string method) =>
        document.RootElement
            .GetProperty("paths")
            .GetProperty(path)
            .GetProperty(method);

    private static async Task<JsonDocument> ReadDocumentAsync(
        MayorxApiFactory factory)
    {
        using var response = await factory.CreateClient().GetAsync(DocumentPath);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        return JsonDocument.Parse(
            await response.Content.ReadAsStringAsync());
    }

    private static async Task<MayorxApiFactory> StartAsync()
    {
        var factory = MayorxApiFactory.WithSettings(
            new Dictionary<string, string?>
            {
                ["RateLimiting:AuthenticationPermitLimit"] = "0"
            });

        factory.UseEnvironmentFor("Development");
        factory.Initialise();

        await factory.MigrateAsync();

        return factory;
    }
}