using System.Net;
using Xunit;

namespace Api.IntegrationTests;

/// <summary>
/// Response headers and the request body ceiling, exercised over real HTTP so the
/// assertions are about what a client actually receives.
/// </summary>
public sealed class SecurityMiddlewareTests : IAsyncLifetime
{
    private const long MaxRequestBodySizeInBytes = 4096;

    private readonly MayorxApiFactory _factory;

    private HttpClient _client = null!;

    public SecurityMiddlewareTests()
    {
        // A small ceiling keeps the oversized-body test cheap. The production
        // default is asserted separately so the knob cannot silently drift.
        _factory = MayorxApiFactory.WithSettings(
            new Dictionary<string, string?>
            {
                ["RequestSizeLimit:MaxRequestBodySizeInBytes"] =
                    MaxRequestBodySizeInBytes.ToString()
            });
    }

    public async Task InitializeAsync()
    {
        _factory.Initialise();
        await _factory.MigrateAsync();

        _client = _factory.CreateClient();
    }

    public Task DisposeAsync()
    {
        _client?.Dispose();
        _factory.Dispose();

        return Task.CompletedTask;
    }

    // ------------------------------------------------------- response headers

    [Fact]
    public async Task Api_responses_carry_the_browser_safety_headers()
    {
        using var response = await _client.GetAsync("api/v1/health/live");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var headers = response.Headers;

        Assert.Equal("nosniff", Header(headers, "X-Content-Type-Options"));
        Assert.Equal("DENY", Header(headers, "X-Frame-Options"));
        Assert.Equal(
            "strict-origin-when-cross-origin",
            Header(headers, "Referrer-Policy"));
    }

    [Fact]
    public async Task The_obsolete_xss_auditor_is_switched_off_explicitly()
    {
        using var response = await _client.GetAsync("api/v1/health/live");

        // Zero rather than absent: an absent header leaves old user agents free
        // to apply a filter with known bypasses.
        Assert.Equal("0", Header(response.Headers, "X-XSS-Protection"));
    }

    [Fact]
    public async Task Api_responses_are_not_cached()
    {
        // A real API route rather than a health check, because the health check
        // writer appends its own no-cache directive and would mask a change here.
        using var response = await _client.PostAsync(
            "api/v1/auth/login",
            new StringContent(
                "{}",
                System.Text.Encoding.UTF8,
                "application/json"));

        // Access tokens and personal content must not be kept in a shared cache
        // or restored from the back button.
        Assert.Equal("no-store", Header(response.Headers, "Cache-Control"));
    }

    [Fact]
    public async Task Headers_are_present_on_error_responses_too()
    {
        // A model binding failure, which produces an error response from a stage
        // later in the pipeline than the headers are written in.
        using var response = await _client.PostAsync(
            "api/v1/auth/login",
            new StringContent("not json", System.Text.Encoding.UTF8, "application/json"));

        Assert.NotEqual(HttpStatusCode.OK, response.StatusCode);

        Assert.Equal("nosniff", Header(response.Headers, "X-Content-Type-Options"));
        Assert.Equal("DENY", Header(response.Headers, "X-Frame-Options"));
        Assert.Equal("no-store", Header(response.Headers, "Cache-Control"));
    }

    [Fact]
    public async Task Headers_are_present_on_a_404()
    {
        using var response = await _client.GetAsync("api/v1/nothing-here");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        Assert.Equal("nosniff", Header(response.Headers, "X-Content-Type-Options"));
        Assert.Equal("DENY", Header(response.Headers, "X-Frame-Options"));
    }

    [Fact]
    public async Task A_content_security_policy_is_not_claimed_for_a_json_api()
    {
        using var response = await _client.GetAsync("api/v1/health/live");

        // The API serves JSON, not documents. Advertising a CSP it cannot honour
        // is worse than not having one, because it reads as a guarantee.
        Assert.False(
            response.Headers.Contains("Content-Security-Policy"),
            "A CSP here would be decorative and misleading.");
    }

    // -------------------------------------------------------- body size limit

    [Fact]
    public async Task A_body_within_the_limit_is_read_normally()
    {
        using var content = JsonContent(
            new { email = "someone@example.test", password = "a-password" });

        using var response = await _client.PostAsync(
            "api/v1/auth/login",
            content);

        // Reached the endpoint, which validated the body and answered 401.
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task A_body_over_the_limit_is_refused_with_an_error_envelope()
    {
        using var content = JsonContent(
            new { padding = new string('x', (int)MaxRequestBodySizeInBytes * 2) });

        using var response = await _client.PostAsync(
            "api/v1/auth/login",
            content);

        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);

        var body = await response.Content.ReadAsStringAsync();

        using var document = System.Text.Json.JsonDocument.Parse(body);

        Assert.Equal(
            "REQUEST_TOO_LARGE",
            document.RootElement.GetProperty("error").GetProperty("code").GetString());

        // The message has to name the ceiling, otherwise a client cannot tell
        // whether it is under or over.
        Assert.Contains(
            MaxRequestBodySizeInBytes.ToString(),
            document.RootElement.GetProperty("error").GetProperty("message").GetString());
    }

    [Fact]
    public async Task An_oversized_body_is_refused_before_the_endpoint_runs()
    {
        using var content = JsonContent(
            new { email = "someone@example.test", password = "a-password" });

        content.Headers.ContentLength = MaxRequestBodySizeInBytes * 4;

        using var response = await _client.PostAsync(
            "api/v1/auth/login",
            content);

        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, response.StatusCode);

        // A validation error from the endpoint would mean the whole body was
        // parsed first, which is exactly what the ceiling exists to prevent.
        Assert.DoesNotContain("INVALID_CREDENTIALS", await response.Content.ReadAsStringAsync());
    }

    // ---------------------------------------------------------------- helpers

    private static string? Header(
        System.Net.Http.Headers.HttpResponseHeaders headers,
        string name) =>
        headers.TryGetValues(name, out var values)
            ? Assert.Single(values)
            : null;

    private static StringContent JsonContent(object value) =>
        new(
            System.Text.Json.JsonSerializer.Serialize(value),
            System.Text.Encoding.UTF8,
            "application/json");
}