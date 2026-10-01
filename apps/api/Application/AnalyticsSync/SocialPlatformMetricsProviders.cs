using System.Net.Http.Headers;
using System.Text.Json;
using Api.Application.SocialAccounts;
using Api.Configuration;
using Api.Domain.Enums;
using Microsoft.Extensions.Options;

namespace Api.Application.AnalyticsSync;

/// <summary>
/// Shared HTTP mechanics for reading post metrics.
/// </summary>
/// <remarks>
/// Every platform read is an authenticated GET, and every one of them can fail
/// in the same ways, so the token handling, failure classification and JSON
/// parsing live here. Failure messages never carry the response body: a metrics
/// response identifies a post, and the request carries a token.
/// </remarks>
public abstract class OAuthSocialPlatformMetricsProvider : ISocialPlatformMetricsProvider
{
    private readonly HttpClient _client;

    protected OAuthSocialPlatformMetricsProvider(HttpClient client)
    {
        ArgumentNullException.ThrowIfNull(client);

        _client = client;
    }

    public abstract ContentPlatform Platform { get; }

    public abstract bool IsConfigured { get; }

    public abstract Task<SocialMetricsSnapshot> FetchAsync(
        string accessToken,
        string externalPostId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Performs one authenticated GET and returns the parsed body.
    /// </summary>
    /// <remarks>
    /// The returned element is detached from its document, so the caller can
    /// read it after this method returns. The operation name only describes the
    /// failure, never the body.
    /// </remarks>
    protected Task<JsonElement> GetJsonAsync(
        string accessToken,
        Uri uri,
        CancellationToken cancellationToken) =>
        GetJsonAsync(accessToken, uri, static _ => { }, cancellationToken);

    /// <summary>
    /// Adds platform-required headers, such as LinkedIn's API version.
    /// </summary>
    protected async Task<JsonElement> GetJsonAsync(
        string accessToken,
        Uri uri,
        Action<HttpRequestMessage> configure,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(accessToken);
        ArgumentNullException.ThrowIfNull(uri);
        ArgumentNullException.ThrowIfNull(configure);

        using var message = new HttpRequestMessage(HttpMethod.Get, uri);
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        message.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        configure(message);

        using var response = await _client.SendAsync(message, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw SocialProviderHttpFailures.FromStatusCode(
                response.StatusCode,
                $"reading {Platform} post metrics");
        }

        try
        {
            await using var body = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var document = await JsonDocument.ParseAsync(
                body,
                cancellationToken: cancellationToken);

            return document.RootElement.Clone();
        }
        catch (JsonException)
        {
            throw new SocialProviderRequestException(
                $"The social platform returned a metrics response for {Platform} that could not be read as JSON.");
        }
    }

    /// <summary>
    /// Reads a nested JSON object, or an undefined element when it is absent, so
    /// callers can pass the result straight to <see cref="ReadCount"/>.
    /// </summary>
    protected static JsonElement ReadObject(JsonElement parent, string propertyName) =>
        parent.ValueKind == JsonValueKind.Object &&
        parent.TryGetProperty(propertyName, out var value) &&
        value.ValueKind == JsonValueKind.Object
            ? value
            : default;

    /// <summary>
    /// Reads a JSON number as a metric, leaving it null when absent or not a
    /// number. A missing value is not the same as zero, so it is never coerced.
    /// </summary>
    protected static long? ReadCount(JsonElement parent, string propertyName) =>
        parent.ValueKind == JsonValueKind.Object &&
        parent.TryGetProperty(propertyName, out var value) &&
        value.ValueKind == JsonValueKind.Number &&
        value.TryGetInt64(out var count)
            ? count
            : null;
}

public sealed class XSocialPlatformMetricsProvider : OAuthSocialPlatformMetricsProvider
{
    private readonly XOptions _options;

    public XSocialPlatformMetricsProvider(HttpClient client, IOptions<XOptions> options)
        : base(client)
    {
        _options = options.Value;
    }

    public override ContentPlatform Platform => ContentPlatform.X;

    public override bool IsConfigured => _options.IsFullyConfigured;

    /// <remarks>
    /// One request returns both metric sets. Private metrics are preferred for
    /// impressions because the public count is rounded and omits the logged out
    /// views that make up most reach.
    /// </remarks>
    public override async Task<SocialMetricsSnapshot> FetchAsync(
        string accessToken,
        string externalPostId,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(externalPostId);

        var builder = new UriBuilder(
            $"{_options.MetricsEndpoint.TrimEnd('/')}/{Uri.EscapeDataString(externalPostId)}")
        {
            Query = "tweet.fields=public_metrics,non_public_metrics"
        };

        var root = await GetJsonAsync(accessToken, builder.Uri, cancellationToken);
        if (root.ValueKind != JsonValueKind.Object ||
            !root.TryGetProperty("data", out var data) ||
            data.ValueKind != JsonValueKind.Object)
        {
            throw new SocialProviderRequestException(
                "X did not return the tweet for the post being measured.");
        }

        var published = ReadObject(data, "public_metrics");
        var nonPublic = ReadObject(data, "non_public_metrics");

        return new SocialMetricsSnapshot(
            ReadCount(nonPublic, "impression_count") ??
                ReadCount(published, "impression_count"),
            ReadCount(published, "like_count"),
            ReadCount(published, "reply_count"),
            // There is one shares column and X counts reposts and quotes
            // separately, so both count as shares rather than dropping either.
            Add(ReadCount(published, "retweet_count"), ReadCount(published, "quote_count")),
            ReadCount(nonPublic, "url_link_clicks"),
            ReadCount(nonPublic, "user_profile_clicks"),
            // X exposes no per-post follower gain, so this stays null rather than
            // being reported as zero.
            FollowersGained: null);
    }

    private static long? Add(long? left, long? right) =>
        left is null && right is null ? null : (left ?? 0) + (right ?? 0);
}

public sealed class LinkedInSocialPlatformMetricsProvider : OAuthSocialPlatformMetricsProvider
{
    private readonly LinkedInOptions _options;

    public LinkedInSocialPlatformMetricsProvider(HttpClient client, IOptions<LinkedInOptions> options)
        : base(client)
    {
        _options = options.Value;
    }

    public override ContentPlatform Platform => ContentPlatform.LinkedIn;

    public override bool IsConfigured => _options.IsFullyConfigured;

    /// <remarks>
    /// <para>
    /// LinkedIn exposes one metric per request, so a single snapshot costs seven
    /// calls. That is the API's shape rather than a choice, and it is why the
    /// sync batch is small and the resync interval is measured in hours.
    /// </para>
    /// <para>
    /// These metrics require the Community Management API product. A deployment
    /// without it gets a rejected request, which surfaces as a provider failure
    /// and leaves the post's metrics empty rather than inventing zeros.
    /// </para>
    /// </remarks>
    public override async Task<SocialMetricsSnapshot> FetchAsync(
        string accessToken,
        string externalPostId,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(externalPostId);

        var entity = BuildEntity(externalPostId);

        return new SocialMetricsSnapshot(
            await ReadMetricAsync(accessToken, entity, "IMPRESSION", cancellationToken),
            await ReadMetricAsync(accessToken, entity, "REACTION", cancellationToken),
            await ReadMetricAsync(accessToken, entity, "COMMENT", cancellationToken),
            await ReadMetricAsync(accessToken, entity, "RESHARE", cancellationToken),
            await ReadMetricAsync(accessToken, entity, "LINK_CLICKS", cancellationToken),
            await ReadMetricAsync(accessToken, entity, "PROFILE_VIEW_FROM_CONTENT", cancellationToken),
            await ReadMetricAsync(accessToken, entity, "FOLLOWER_GAINED_FROM_CONTENT", cancellationToken));
    }

    /// <summary>
    /// Wraps a published post URN in the entity form the analytics finder
    /// expects. The publish API returns a share URN, so the prefix follows the
    /// share resource LinkedIn actually created.
    /// </summary>
    private static string BuildEntity(string externalPostId)
    {
        var urn = externalPostId.StartsWith("urn:li:", StringComparison.Ordinal)
            ? externalPostId
            : $"urn:li:share:{externalPostId}";

        var prefix = urn.Contains(":ugcPost:", StringComparison.Ordinal) ? "ugc" : "share";

        return $"({prefix}:{urn})";
    }

    private async Task<long?> ReadMetricAsync(
        string accessToken,
        string entity,
        string queryType,
        CancellationToken cancellationToken)
    {
        var builder = new UriBuilder(_options.MetricsEndpoint)
        {
            Query = $"q=entity&entity={Uri.EscapeDataString(entity)}" +
                $"&queryType={queryType}&aggregation=TOTAL&timeGranularity=ALL"
        };

        var root = await GetJsonAsync(
            accessToken,
            builder.Uri,
            message =>
            {
                // LinkedIn still requires the Rest.li protocol version, and the
                // API version is chosen per call by header.
                message.Headers.TryAddWithoutValidation(
                    "X-Restli-Protocol-Version",
                    "2.0.0");
                message.Headers.TryAddWithoutValidation(
                    "LinkedIn-Version",
                    _options.MetricsVersion);
            },
            cancellationToken);

        // A metric LinkedIn has no result for comes back as an empty elements
        // array rather than an error, and becomes a null rather than a zero.
        if (root.ValueKind != JsonValueKind.Object ||
            !root.TryGetProperty("elements", out var elements) ||
            elements.ValueKind != JsonValueKind.Array)
        {
            throw new SocialProviderRequestException(
                $"LinkedIn did not return a {queryType} result for the post being measured.");
        }

        foreach (var element in elements.EnumerateArray())
        {
            if (ReadCount(element, "count") is { } count)
            {
                return count;
            }
        }

        return null;
    }
}
