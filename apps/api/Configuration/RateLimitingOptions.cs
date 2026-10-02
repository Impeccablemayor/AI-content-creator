namespace Api.Configuration;

/// <summary>
/// Throttling for the authentication endpoints.
/// </summary>
/// <remarks>
/// The limit is per client address and shared across API instances, because it
/// is counted in Redis rather than in process memory.
/// </remarks>
public sealed class RateLimitingOptions
{
    public const string SectionName = "RateLimiting";

    /// <summary>
    /// Requests allowed per window per client address. Zero or less disables
    /// throttling entirely.
    /// </summary>
    public int AuthenticationPermitLimit { get; set; } = 20;

    /// <summary>
    /// Length of the window. Anchored to the first request in it.
    /// </summary>
    public TimeSpan AuthenticationWindow { get; set; } = TimeSpan.FromMinutes(1);

    /// <summary>
    /// Prefix for the Redis keys that hold the counters.
    /// </summary>
    /// <remarks>
    /// Configurable because one Redis instance can serve more than one
    /// deployment, and because a caller that needs its own counters to be
    /// independent of everyone else's has no other way to say so. The default
    /// keeps the counters recognisable in <c>redis-cli</c>.
    /// </remarks>
    public string KeyPrefix { get; set; } = "mayorx:ratelimit";
}
