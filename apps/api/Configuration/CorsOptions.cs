namespace Api.Configuration;

/// <summary>
/// Cross-origin origins permitted to call the API from a browser.
/// </summary>
/// <remarks>
/// The repository contains no frontend and documents no origin, so none is
/// guessed. An origin list is configuration, and outside Development it is
/// required: the API refuses to start rather than silently shipping a
/// development default into production.
/// </remarks>
public sealed class CorsOptions
{
    public const string SectionName = "Cors";

    /// <summary>
    /// Exact origins allowed, for example <c>https://app.example.com</c>. No
    /// wildcards: a wildcard cannot be combined with credentials, and allowing
    /// every origin would defeat the point.
    /// </summary>
    public string[] AllowedOrigins { get; set; } = [];
}
