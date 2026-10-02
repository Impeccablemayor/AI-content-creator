using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace Api.Configuration.Swagger;

/// <summary>
/// Decides whether the OpenAPI document and its UI are served.
/// </summary>
/// <remarks>
/// Swagger is development tooling. Serving it in production publishes the whole
/// API surface, which is reconnaissance for free, so it is gated on the
/// environment. Development is left exactly as it was, and an explicit
/// opt-in exists for the case where a deployment genuinely needs the document.
/// </remarks>
public static class SwaggerExposure
{
    /// <summary>Path the OpenAPI JSON document is served from.</summary>
    public const string DocumentPath = "/swagger/v1/swagger.json";

    /// <summary>Path the interactive UI is served from.</summary>
    public const string UiPath = "/swagger";

    /// <summary>
    /// Whether Swagger should be served for this host.
    /// </summary>
    /// <param name="environment">The hosting environment name.</param>
    /// <param name="configuration">Configuration, for the explicit opt-in.</param>
    public static bool ShouldServe(
        IHostEnvironment environment,
        IConfiguration configuration)
    {
        var optedIn = configuration.GetValue<bool?>("Swagger:Enabled");

        if (optedIn.HasValue)
        {
            return optedIn.Value;
        }

        return environment.IsDevelopment();
    }
}
