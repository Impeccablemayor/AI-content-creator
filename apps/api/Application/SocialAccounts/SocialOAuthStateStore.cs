using System.Security.Cryptography;
using System.Text.Json;
using Api.Domain.Enums;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Options;

namespace Api.Application.SocialAccounts;

/// <summary>
/// A pending, single-use OAuth authorization request.
/// </summary>
/// <param name="WorkspaceId">
/// The workspace that initiated the flow. The callback is anonymous, so this
/// binding is the only thing that authorises the connection.
/// </param>
public sealed record SocialOAuthState(
    Guid WorkspaceId,
    ContentPlatform Platform,
    string? CodeVerifier);

public interface ISocialOAuthStateStore
{
    Task StoreAsync(string state, SocialOAuthState value, CancellationToken cancellationToken);

    /// <summary>
    /// Reads and invalidates a state value. Returns null when the value is
    /// unknown, expired, or already used.
    /// </summary>
    Task<SocialOAuthState?> ConsumeAsync(string state, CancellationToken cancellationToken);
}

/// <summary>
/// Stores OAuth state in the shared cache so it survives restarts and works
/// across API instances.
/// </summary>
public sealed class DistributedSocialOAuthStateStore : ISocialOAuthStateStore
{
    private const string KeyPrefix = "social:oauth-state:";

    private readonly IDistributedCache _cache;
    private readonly IOptions<Configuration.SocialPlatformOptions> _options;

    public DistributedSocialOAuthStateStore(
        IDistributedCache cache,
        IOptions<Configuration.SocialPlatformOptions> options)
    {
        _cache = cache;
        _options = options;
    }

    public async Task StoreAsync(
        string state,
        SocialOAuthState value,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(state);
        ArgumentNullException.ThrowIfNull(value);

        var payload = JsonSerializer.Serialize(value);

        await _cache.SetStringAsync(
            KeyPrefix + state,
            payload,
            new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow =
                    TimeSpan.FromMinutes(_options.Value.StateLifetimeMinutes)
            });
    }

    public async Task<SocialOAuthState?> ConsumeAsync(
        string state,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(state))
        {
            return null;
        }

        var key = KeyPrefix + state;
        var payload = await _cache.GetStringAsync(key);
        if (payload is null)
        {
            return null;
        }

        // Invalidate before use so a replayed callback cannot reuse the value.
        await _cache.RemoveAsync(key);

        try
        {
            return JsonSerializer.Deserialize<SocialOAuthState>(payload);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}

public static class SocialOAuthStateFactory
{
    public static (string State, string? CodeVerifier) Create(ContentPlatform platform)
    {
        var state = Base64UrlEncode(RandomNumberGenerator.GetBytes(32));

        // X requires PKCE; LinkedIn's authorization-code flow does not use it.
        return platform == ContentPlatform.X
            ? (state, SocialPkce.CreateCodeVerifier())
            : (state, null);
    }

    private static string Base64UrlEncode(byte[] value) =>
        Convert.ToBase64String(value)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
}
