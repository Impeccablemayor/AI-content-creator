using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text.Json;
using Api.Configuration;
using Microsoft.AspNetCore.WebUtilities;

namespace Api.Application.SocialAccounts;

/// <summary>
/// Shared OAuth 2.0 authorization-code mechanics for social platforms.
/// </summary>
/// <remarks>
/// Failure paths deliberately never carry the provider response body, because
/// a token endpoint body can contain credentials.
/// </remarks>
public abstract class OAuthSocialPlatformProvider : ISocialPlatformProvider
{
    private readonly HttpClient _client;
    private readonly SocialPlatformClientOptions _options;
    private readonly string _authorizationEndpoint;
    private readonly string _tokenEndpoint;

    protected OAuthSocialPlatformProvider(
        HttpClient client,
        SocialPlatformClientOptions options,
        string authorizationEndpoint,
        string tokenEndpoint)
    {
        _client = client;
        _options = options;
        _authorizationEndpoint = authorizationEndpoint;
        _tokenEndpoint = tokenEndpoint;
    }

    public abstract Domain.Enums.ContentPlatform Platform { get; }

    public bool IsConfigured => _options.IsFullyConfigured;

    public virtual Uri BuildAuthorizationUri(string state, string? codeChallenge)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(state);

        var query = new Dictionary<string, string?>
        {
            ["response_type"] = "code",
            ["client_id"] = _options.ClientId,
            ["redirect_uri"] = _options.RedirectUri,
            ["scope"] = string.Join(' ', _options.Scopes),
            ["state"] = state
        };

        if (codeChallenge is not null)
        {
            query["code_challenge"] = codeChallenge;
            query["code_challenge_method"] = "S256";
        }

        return new Uri(QueryHelpers.AddQueryString(_authorizationEndpoint, query));
    }

    public async Task<SocialTokenResponse> ExchangeAuthorizationCodeAsync(
        string code,
        string? codeVerifier,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);

        var fields = new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["code"] = code,
            ["redirect_uri"] = _options.RedirectUri,
            ["client_id"] = _options.ClientId,
            ["client_secret"] = _options.ClientSecret
        };

        if (codeVerifier is not null)
        {
            fields["code_verifier"] = codeVerifier;
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, _tokenEndpoint)
        {
            Content = new FormUrlEncodedContent(fields)
        };

        using var response = await _client.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw CreateProviderFailure(
                response.StatusCode,
                SocialProviderOperation.OAuthToken);
        }

        await using var body = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(body, cancellationToken: cancellationToken);
        var root = document.RootElement;

        if (!root.TryGetProperty("access_token", out var accessTokenElement) ||
            accessTokenElement.ValueKind != JsonValueKind.String ||
            string.IsNullOrWhiteSpace(accessTokenElement.GetString()))
        {
            throw new SocialProviderRequestException(
                "The OAuth token endpoint did not return an access token.");
        }

        var refreshToken = root.TryGetProperty("refresh_token", out var refreshElement) &&
            refreshElement.ValueKind == JsonValueKind.String
            ? refreshElement.GetString()
            : null;

        DateTime? expiresAt = null;
        if (root.TryGetProperty("expires_in", out var expiresElement) &&
            expiresElement.TryGetInt32(out var expiresInSeconds) &&
            expiresInSeconds > 0)
        {
            expiresAt = DateTime.UtcNow.AddSeconds(expiresInSeconds);
        }

        var scopes = root.TryGetProperty("scope", out var scopeElement) &&
            scopeElement.ValueKind == JsonValueKind.String
            ? scopeElement.GetString()!
                .Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .ToList()
            : _options.Scopes.ToList();

        return new SocialTokenResponse(
            accessTokenElement.GetString()!,
            string.IsNullOrWhiteSpace(refreshToken) ? null : refreshToken,
            expiresAt,
            scopes);
    }

    public abstract Task<SocialAccountProfile> GetProfileAsync(
        string accessToken,
        CancellationToken cancellationToken);

    protected async Task<JsonDocument> GetProfileDocumentAsync(
        string accessToken,
        string profileEndpoint,
        string? query,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(accessToken);

        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            query is null ? profileEndpoint : $"{profileEndpoint}{query}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        using var response = await _client.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw CreateProviderFailure(
                response.StatusCode,
                SocialProviderOperation.OAuthProfile);
        }

        var body = await response.Content.ReadAsStreamAsync(cancellationToken);
        return await JsonDocument.ParseAsync(body, cancellationToken: cancellationToken);
    }

    private Exception CreateProviderFailure(
        HttpStatusCode statusCode,
        SocialProviderOperation operation)
    {
        // Counted here, next to the classification, because this is the only place
        // an OAuth failure is described. The classification below is narrower than
        // the publishing one on purpose: an OAuth failure is always a problem with
        // the stored configuration, so only the provider's own answer is reported.
        Infrastructure.Metrics.ApplicationMetrics.RecordProviderFailure(
            Platform,
            operation,
            statusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden
                ? SocialProviderFailureClass.Authentication
                : SocialProviderFailureClass.Request);

        return statusCode switch
        {
            HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden =>
                new SocialProviderAuthenticationException(
                    $"The social platform rejected the configured credentials when calling {Describe(operation)}."),
            _ => new SocialProviderRequestException(
                $"The social platform returned HTTP {(int)statusCode} from {Describe(operation)}.")
        };
    }

    private static string Describe(SocialProviderOperation operation) =>
        operation == SocialProviderOperation.OAuthToken
            ? "the OAuth token endpoint"
            : "the platform profile endpoint";

    protected static string? ReadString(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var value) &&
            value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
    }
}

public static class SocialPkce
{
    /// <summary>
    /// Generates an RFC 7636 code verifier.
    /// </summary>
    public static string CreateCodeVerifier()
    {
        Span<byte> bytes = stackalloc byte[32];
        RandomNumberGenerator.Fill(bytes);

        return Base64UrlEncode(bytes);
    }

    /// <summary>
    /// Derives the S256 code challenge for a verifier.
    /// </summary>
    public static string CreateCodeChallenge(string codeVerifier)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(codeVerifier);

        var hash = SHA256.HashData(System.Text.Encoding.ASCII.GetBytes(codeVerifier));

        return Base64UrlEncode(hash);
    }

    private static string Base64UrlEncode(ReadOnlySpan<byte> value) =>
        Convert.ToBase64String(value)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
}
