using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Api.Application.SocialAccounts;
using Api.Configuration;
using Api.Domain.Enums;
using Microsoft.Extensions.Options;

namespace Api.Application.Publishing;

/// <summary>
/// Shared HTTP mechanics for social publishing.
/// </summary>
/// <remarks>
/// Failure messages deliberately never carry the provider response body, because
/// a publish response can echo the submitted content and, on some platforms, a
/// token. Only the status code and the endpoint are reported.
/// </remarks>
public abstract class OAuthSocialPlatformPublisher : ISocialPlatformPublisher
{
    private readonly HttpClient _client;
    private readonly string _publishEndpoint;

    protected OAuthSocialPlatformPublisher(HttpClient client, string publishEndpoint)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentException.ThrowIfNullOrWhiteSpace(publishEndpoint);

        _client = client;
        _publishEndpoint = publishEndpoint;
    }

    public abstract ContentPlatform Platform { get; }

    public abstract bool IsConfigured { get; }

    public async Task<SocialPublishResult> PublishAsync(
        string accessToken,
        SocialPublishRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(accessToken);
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.Text))
        {
            throw new SocialProviderRequestException(
                "The content post has no publishable text.");
        }

        using var message = new HttpRequestMessage(HttpMethod.Post, _publishEndpoint)
        {
            Content = new StringContent(
                BuildPayload(request),
                Encoding.UTF8,
                "application/json")
        };
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        message.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        using var response = await _client.SendAsync(message, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw SocialProviderHttpFailures.FromStatusCode(
                response.StatusCode,
                SocialProviderOperation.Publish,
                Platform);
        }

        JsonDocument document;
        try
        {
            await using var body = await response.Content.ReadAsStreamAsync(cancellationToken);
            document = await JsonDocument.ParseAsync(body, cancellationToken: cancellationToken);
        }
        catch (JsonException)
        {
            // The body is never included: a 2xx publish response can still echo
            // submitted content.
            throw new SocialProviderRequestException(
                "The social platform returned a success response that could not be read as JSON.");
        }

        using (document)
        {
            return ReadResult(document.RootElement);
        }
    }

    /// <summary>
    /// Builds the platform-specific publish body.
    /// </summary>
    protected abstract string BuildPayload(SocialPublishRequest request);

    /// <summary>
    /// Reads the created post's identifier out of a success response.
    /// </summary>
    protected abstract SocialPublishResult ReadResult(JsonElement root);
}

public sealed class LinkedInSocialPlatformPublisher : OAuthSocialPlatformPublisher
{
    private readonly LinkedInOptions _options;

    public LinkedInSocialPlatformPublisher(HttpClient client, IOptions<LinkedInOptions> options)
        : base(client, options.Value.PublishEndpoint)
    {
        _options = options.Value;
    }

    public override ContentPlatform Platform => ContentPlatform.LinkedIn;

    public override bool IsConfigured => _options.IsFullyConfigured;

    /// <summary>
    /// LinkedIn's share API requires the author URN in the body. It is built from
    /// the account identifier the connection flow already stored, so the bearer
    /// token never enters the request body.
    /// </summary>
    protected override string BuildPayload(SocialPublishRequest request) =>
        $$"""
          {
            "author": {{JsonSerializer.Serialize($"urn:li:person:{request.PlatformAccountId}")}},
            "lifecycleState": "PUBLISHED",
            "specificContent": {
              "com.linkedin.ugc.ShareContent": {
                "shareCommentary": { "text": {{JsonSerializer.Serialize(request.Text)}} },
                "shareMediaCategory": "NONE"
              }
            },
            "visibility": {
              "com.linkedin.ugc.MemberNetworkVisibility": "PUBLIC"
            }
          }
          """;

    protected override SocialPublishResult ReadResult(JsonElement root)
    {
        if (!root.TryGetProperty("id", out var id) ||
            id.ValueKind != JsonValueKind.String ||
            string.IsNullOrWhiteSpace(id.GetString()))
        {
            throw new SocialProviderRequestException(
                "LinkedIn did not return a post identifier for the published content.");
        }

        return new SocialPublishResult(id.GetString()!, null);
    }
}

public sealed class XSocialPlatformPublisher : OAuthSocialPlatformPublisher
{
    private readonly XOptions _options;

    public XSocialPlatformPublisher(HttpClient client, IOptions<XOptions> options)
        : base(client, options.Value.PublishEndpoint)
    {
        _options = options.Value;
    }

    public override ContentPlatform Platform => ContentPlatform.X;

    public override bool IsConfigured => _options.IsFullyConfigured;

    /// <remarks>
    /// X derives the posting account from the bearer token, so the stored
    /// account identifier is not needed in the body.
    /// </remarks>
    protected override string BuildPayload(SocialPublishRequest request) =>
        $$"""
          { "text": {{JsonSerializer.Serialize(request.Text)}} }
          """;

    protected override SocialPublishResult ReadResult(JsonElement root)
    {
        if (!root.TryGetProperty("data", out var data) ||
            data.ValueKind != JsonValueKind.Object ||
            !data.TryGetProperty("id", out var id) ||
            id.ValueKind != JsonValueKind.String ||
            string.IsNullOrWhiteSpace(id.GetString()))
        {
            throw new SocialProviderRequestException(
                "X did not return a post identifier for the published content.");
        }

        var tweetId = id.GetString()!;

        return new SocialPublishResult(tweetId, $"https://x.com/i/web/status/{tweetId}");
    }
}
