using System.Net;
using Xunit;

namespace Api.IntegrationTests;

/// <summary>
/// Where the API documentation may be served from.
/// </summary>
/// <remarks>
/// An accidentally published OpenAPI document advertises every route this API
/// has, including the ones nobody intended to be findable, so the interesting
/// property is not that Swagger works but that it is absent unless asked for.
/// </remarks>
public sealed class SwaggerExposureTests
{
    private const string OpenApiDocumentPath = "swagger/v1/swagger.json";

    [Fact]
    public async Task The_document_is_not_served_outside_development()
    {
        using var factory = await StartAsync(environment: "Production");

        using var response = await factory.CreateClient()
            .GetAsync(OpenApiDocumentPath);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task The_document_is_not_served_when_only_testing()
    {
        // The environment the rest of the suite runs in. If this ever starts
        // serving Swagger, the other suites are running against a
        // differently-configured host than production sees.
        using var factory = await StartAsync(environment: "Testing");

        using var response = await factory.CreateClient()
            .GetAsync(OpenApiDocumentPath);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task The_document_is_served_in_development()
    {
        using var factory = await StartAsync(environment: "Development");

        using var response = await factory.CreateClient()
            .GetAsync(OpenApiDocumentPath);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();

        using var document = System.Text.Json.JsonDocument.Parse(body);

        Assert.True(
            document.RootElement.GetProperty("paths").TryGetProperty(
                "/api/v1/auth/login",
                out _),
            "The document should describe the real routes.");
    }

    [Fact]
    public async Task The_interface_is_served_in_development()
    {
        using var factory = await StartAsync(environment: "Development");

        using var response = await factory.CreateClient()
            .GetAsync("swagger/index.html");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task The_interface_is_not_served_outside_development()
    {
        using var factory = await StartAsync(environment: "Production");

        using var response = await factory.CreateClient()
            .GetAsync("swagger/index.html");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Production_can_opt_in_deliberately()
    {
        using var factory = await StartAsync(
            environment: "Production",
            settings: new Dictionary<string, string?>
            {
                ["Swagger:Enabled"] = "true"
            });

        using var response = await factory.CreateClient()
            .GetAsync(OpenApiDocumentPath);

        // Exposed because someone asked, not because it defaulted on. An operator
        // who wants it must be able to turn it on, and that has to be visible.
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Production_stays_closed_when_the_opt_in_is_false()
    {
        using var factory = await StartAsync(
            environment: "Production",
            settings: new Dictionary<string, string?>
            {
                ["Swagger:Enabled"] = "false"
            });

        using var response = await factory.CreateClient()
            .GetAsync(OpenApiDocumentPath);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public void A_nonsense_opt_in_value_refuses_to_start_rather_than_guessing()
    {
        using var factory = MayorxApiFactory.WithSettings(
            new Dictionary<string, string?>
            {
                ["Swagger:Enabled"] = "yes please"
            });

        factory.Initialise();

        // A typo in a security gate must not be guessed either way. Refusing to
        // start is the honest outcome: the operator is told their configuration
        // is wrong instead of being left to wonder which way it fell.
        var failure = Assert.Throws<InvalidOperationException>(
            () => factory.CreateClient().GetAsync(OpenApiDocumentPath).GetAwaiter().GetResult());

        Assert.Contains("Swagger:Enabled", failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task An_explicit_false_closes_the_document_even_in_development()
    {
        using var factory = await StartAsync(
            environment: "Development",
            settings: new Dictionary<string, string?>
            {
                ["Swagger:Enabled"] = "false"
            });

        using var response = await factory.CreateClient()
            .GetAsync(OpenApiDocumentPath);

        // An explicit setting wins in every environment, which is why the
        // development settings file states Enabled true rather than relying on
        // the environment alone. The developer default lives in configuration
        // where it can be seen.
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Development_serves_the_document_when_nothing_is_configured()
    {
        // The environment default, with no opt-in of any kind. This is the
        // behaviour a developer with no configuration gets.
        using var factory = MayorxApiFactory.WithSettings(
            new Dictionary<string, string?>
            {
                // Blanking the inherited value, because the base file sets it.
                ["Swagger:Enabled"] = null
            });

        factory.UseEnvironmentFor("Development");
        factory.Initialise();

        await factory.MigrateAsync();

        using var response = await factory.CreateClient()
            .GetAsync(OpenApiDocumentPath);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private static async Task<MayorxApiFactory> StartAsync(
        string environment,
        IReadOnlyDictionary<string, string?>? settings = null)
    {
        var factory = MayorxApiFactory.WithSettings(settings);

        factory.UseEnvironmentFor(environment);
        factory.Initialise();

        await factory.MigrateAsync();

        return factory;
    }
}