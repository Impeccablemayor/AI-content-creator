using Api.Configuration;

namespace Api.Application.SocialAccounts;

/// <summary>
/// Client configuration rules for a social platform.
/// </summary>
/// <remarks>
/// Shared by the API and the worker so a deployment cannot pass startup
/// validation in one process and fail in the other.
/// </remarks>
public static class SocialPlatformProviderRegistrationRules
{
    public static bool IsValidLinkedIn(LinkedInOptions options) =>
        IsValidClientConfiguration(options) &&
        HasAbsoluteHttpsEndpoints(
            options.AuthorizationEndpoint,
            options.TokenEndpoint,
            options.ProfileEndpoint,
            options.PublishEndpoint,
            options.MetricsEndpoint) &&
        !string.IsNullOrWhiteSpace(options.MetricsVersion);

    public static bool IsValidX(XOptions options) =>
        IsValidClientConfiguration(options) &&
        HasAbsoluteHttpsEndpoints(
            options.AuthorizationEndpoint,
            options.TokenEndpoint,
            options.ProfileEndpoint,
            options.PublishEndpoint,
            options.MetricsEndpoint);

    /// <summary>
    /// A platform that is not configured at all is allowed, so a deployment can
    /// run without social connections. A partially configured platform is not:
    /// it would appear usable and then fail mid-flow.
    /// </summary>
    private static bool IsValidClientConfiguration(SocialPlatformClientOptions options) =>
        !options.IsFullyConfigured
            ? string.IsNullOrWhiteSpace(options.ClientId) &&
                string.IsNullOrWhiteSpace(options.ClientSecret) &&
                string.IsNullOrWhiteSpace(options.RedirectUri)
            : IsAbsoluteHttpsOrLoopback(options.RedirectUri) &&
                options.Scopes.All(scope => !string.IsNullOrWhiteSpace(scope)) &&
                options.TimeoutSeconds is >= 1 and <= 120;

    /// <summary>
    /// Endpoints are bound from configuration, so a typo would otherwise surface
    /// only as a mid-flow provider failure. Tokens are exchanged over the wire,
    /// so they must be HTTPS.
    /// </summary>
    private static bool HasAbsoluteHttpsEndpoints(params string[] endpoints) =>
        endpoints.All(endpoint =>
            Uri.TryCreate(endpoint, UriKind.Absolute, out var uri) &&
            uri.Scheme == Uri.UriSchemeHttps);

    private static bool IsAbsoluteHttpsOrLoopback(string value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri) &&
        (uri.Scheme == Uri.UriSchemeHttps ||
            (uri.Scheme == Uri.UriSchemeHttp && uri.IsLoopback));
}
