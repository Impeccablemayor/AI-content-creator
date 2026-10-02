using System.Net;
using System.Net.Http.Headers;
using Xunit;

namespace Api.IntegrationTests;

/// <summary>
/// Which browser origins may call the API, and whether a deployment without a
/// configured origin refuses to start.
/// </summary>
/// <remarks>
/// The dangerous outcomes here are a wildcard origin, a wildcard combined with
/// credentials, and a silently permissive policy, because all three let any
/// site on the internet read a user's authenticated responses. Each is checked
/// directly rather than inferred from the policy being correct.
/// </remarks>
public sealed class CorsTests
{
    private const string AllowedOrigin = "https://app.example.test";
    private const string OtherOrigin = "https://evil.example.test";

    [Fact]
    public async Task A_configured_origin_is_allowed_and_answered()
    {
        await using var harness = await Harness.StartAsync(
            origins: [AllowedOrigin]);

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            "api/v1/auth/login");

        request.Headers.Add("Origin", AllowedOrigin);

        using var response = await harness.Client.SendAsync(request);

        Assert.Equal(
            AllowedOrigin,
            Single(response.Headers.GetValues("Access-Control-Allow-Origin")));
    }

    [Fact]
    public async Task An_origin_that_was_not_configured_is_not_allowed()
    {
        await using var harness = await Harness.StartAsync(
            origins: [AllowedOrigin]);

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            "api/v1/auth/login");

        request.Headers.Add("Origin", OtherOrigin);

        using var response = await harness.Client.SendAsync(request);

        Assert.False(
            response.Headers.Contains("Access-Control-Allow-Origin"),
            "An unconfigured origin must not be reflected back as allowed.");
    }

    [Fact]
    public async Task Credentials_are_permitted_for_a_configured_origin()
    {
        await using var harness = await Harness.StartAsync(
            origins: [AllowedOrigin]);

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            "api/v1/auth/login");

        request.Headers.Add("Origin", AllowedOrigin);

        using var response = await harness.Client.SendAsync(request);

        // The API authenticates with a bearer token held by the frontend, so
        // credentialed requests are part of the design.
        Assert.Equal(
            "true",
            Single(response.Headers.GetValues("Access-Control-Allow-Credentials")));
    }

    [Fact]
    public async Task The_allow_origin_header_is_never_a_wildcard()
    {
        await using var harness = await Harness.StartAsync(
            origins: [AllowedOrigin]);

        foreach (var origin in new[] { AllowedOrigin, OtherOrigin })
        {
            using var request = new HttpRequestMessage(
                HttpMethod.Post,
                "api/v1/auth/login");

            request.Headers.Add("Origin", origin);

            using var response = await harness.Client.SendAsync(request);

            if (response.Headers.TryGetValues(
                    "Access-Control-Allow-Origin",
                    out var values))
            {
                Assert.DoesNotContain("*", values);
            }
        }
    }

    [Fact]
    public async Task A_preflight_from_a_configured_origin_is_accepted()
    {
        await using var harness = await Harness.StartAsync(
            origins: [AllowedOrigin]);

        using var request = new HttpRequestMessage(
            HttpMethod.Options,
            "api/v1/auth/login");

        request.Headers.Add("Origin", AllowedOrigin);
        request.Headers.Add("Access-Control-Request-Method", "POST");
        request.Headers.Add("Access-Control-Request-Headers", "authorization,content-type");

        using var response = await harness.Client.SendAsync(request);

        Assert.Equal(
            AllowedOrigin,
            Single(response.Headers.GetValues("Access-Control-Allow-Origin")));

        var methods = Single(response.Headers.GetValues("Access-Control-Allow-Methods"));

        Assert.Contains("POST", methods, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task A_preflight_from_an_unknown_origin_is_refused()
    {
        await using var harness = await Harness.StartAsync(
            origins: [AllowedOrigin]);

        using var request = new HttpRequestMessage(
            HttpMethod.Options,
            "api/v1/auth/login");

        request.Headers.Add("Origin", OtherOrigin);
        request.Headers.Add("Access-Control-Request-Method", "POST");

        using var response = await harness.Client.SendAsync(request);

        Assert.False(response.Headers.Contains("Access-Control-Allow-Origin"));
    }

    [Fact]
    public async Task An_empty_origin_list_refuses_every_browser_origin()
    {
        // Development with nothing configured behaves like production rather than
        // opening the door: the policy exists and allows nothing.
        await using var harness = await Harness.StartAsync(
            origins: []);

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            "api/v1/auth/login");

        request.Headers.Add("Origin", AllowedOrigin);

        using var response = await harness.Client.SendAsync(request);

        Assert.False(response.Headers.Contains("Access-Control-Allow-Origin"));
    }

    [Fact]
    public async Task Several_origins_are_all_allowed()
    {
        await using var harness = await Harness.StartAsync(
            origins: [AllowedOrigin, "https://admin.example.test"]);

        foreach (var origin in new[] { AllowedOrigin, "https://admin.example.test" })
        {
            using var request = new HttpRequestMessage(
                HttpMethod.Post,
                "api/v1/auth/login");

            request.Headers.Add("Origin", origin);

            using var response = await harness.Client.SendAsync(request);

            Assert.Equal(
                origin,
                Single(response.Headers.GetValues("Access-Control-Allow-Origin")));
        }
    }

    [Fact]
    public async Task A_host_that_configures_no_origin_refuses_to_start()
    {
        var settings = new Dictionary<string, string?>
        {
            ["Cors:AllowedOrigins:0"] = string.Empty,
            ["Cors:AllowedOrigins:1"] = "   "
        };

        using var factory = MayorxApiFactory.WithSettings(settings);

        // Failing to start is the point. A missing origin that defaulted to "allow
        // everything" would be a silent, catastrophic misconfiguration, so the
        // deployment is refused instead.
        var failure = Assert.Throws<InvalidOperationException>(
            () => factory.CreateClient().GetAsync("api/v1/health/live").GetAwaiter().GetResult());

        Assert.Contains("Cors:AllowedOrigins", failure.Message, StringComparison.Ordinal);
    }

    private static string Single(IEnumerable<string> values) =>
        Assert.Single(values);

    /// <summary>
    /// Owns a host configured with a specific origin list, and keeps the
    /// per-test database lifecycle in one place.
    /// </summary>
    private sealed class Harness : IAsyncDisposable
    {
        private readonly MayorxApiFactory _factory;

        private Harness(MayorxApiFactory factory, HttpClient client)
        {
            _factory = factory;
            Client = client;
        }

        public HttpClient Client { get; }

        public static async Task<Harness> StartAsync(string[] origins)
        {
            var settings = new Dictionary<string, string?>();

            for (var index = 0; index < origins.Length; index++)
            {
                settings[$"Cors:AllowedOrigins:{index}"] = origins[index];
            }

            var factory = MayorxApiFactory.WithSettings(settings);

            factory.Initialise();
            await factory.MigrateAsync();

            return new Harness(factory, factory.CreateClient());
        }

        public ValueTask DisposeAsync()
        {
            Client.Dispose();
            _factory.Dispose();

            return ValueTask.CompletedTask;
        }
    }
}
