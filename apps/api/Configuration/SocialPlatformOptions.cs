namespace Api.Configuration;

public sealed class SocialPlatformOptions
{
    public const string SectionName = "Social";

    /// <summary>
    /// Lifetime of a single-use OAuth state value.
    /// </summary>
    public int StateLifetimeMinutes { get; set; } = 10;

    public LinkedInOptions LinkedIn { get; set; } = new();

    public XOptions X { get; set; } = new();
}

public class SocialPlatformClientOptions
{
    public string ClientId { get; set; } = string.Empty;

    public string ClientSecret { get; set; } = string.Empty;

    public string RedirectUri { get; set; } = string.Empty;

    public List<string> Scopes { get; set; } = [];

    public int TimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// True only when every value required to run an OAuth exchange is present.
    /// A partially configured platform must never appear usable.
    /// </summary>
    public bool IsFullyConfigured =>
        !string.IsNullOrWhiteSpace(ClientId) &&
        !string.IsNullOrWhiteSpace(ClientSecret) &&
        !string.IsNullOrWhiteSpace(RedirectUri) &&
        Scopes.Count > 0;
}

public sealed class LinkedInOptions : SocialPlatformClientOptions
{
    public const string SectionName = "Social:LinkedIn";

    public string AuthorizationEndpoint { get; set; } =
        "https://www.linkedin.com/oauth/v2/authorization";

    public string TokenEndpoint { get; set; } =
        "https://www.linkedin.com/oauth/v2/accessToken";

    public string ProfileEndpoint { get; set; } =
        "https://api.linkedin.com/v2/userinfo";

    public string PublishEndpoint { get; set; } =
        "https://api.linkedin.com/v2/ugcPosts";

    /// <summary>
    /// Base address for member post analytics. Reading these metrics requires
    /// LinkedIn's Community Management API product, so a deployment without it
    /// will get rejected here rather than silently reporting no numbers.
    /// </summary>
    public string MetricsEndpoint { get; set; } =
        "https://api.linkedin.com/rest/memberCreatorPostAnalytics";

    /// <summary>
    /// Value sent as the <c>LinkedIn-Version</c> header. The member post
    /// analytics endpoints do not exist before version 202601.
    /// </summary>
    public string MetricsVersion { get; set; } = "202601";
}

public sealed class XOptions : SocialPlatformClientOptions
{
    public const string SectionName = "Social:X";

    public string AuthorizationEndpoint { get; set; } =
        "https://twitter.com/i/oauth2/authorize";

    public string TokenEndpoint { get; set; } =
        "https://api.twitter.com/2/oauth2/token";

    public string ProfileEndpoint { get; set; } =
        "https://api.twitter.com/2/users/me";

    public string PublishEndpoint { get; set; } =
        "https://api.twitter.com/2/tweets";

    /// <summary>
    /// Base address for reading a published tweet's metrics. Private metrics
    /// need a user-context token, which is what the connection flow stores.
    /// </summary>
    public string MetricsEndpoint { get; set; } =
        "https://api.twitter.com/2/tweets";
}
