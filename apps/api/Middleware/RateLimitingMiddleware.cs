using System.Globalization;
using System.Text.Json;
using Api.Configuration;
using Api.Contracts.Responses;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace Api.Middleware;

/// <summary>
/// Throttles the authentication endpoints per client address, using the Redis
/// instance the API already depends on, so the limit holds across every API
/// instance rather than resetting per process.
/// </summary>
/// <remarks>
/// <para>
/// A fixed window is anchored to the first request in it rather than to the
/// clock, because the count and its expiry are written together and the key
/// expires on its own. Anchoring to the clock instead would let a client send
/// its whole allowance either side of a minute boundary and double it.
/// </para>
/// <para>
/// <b>Redis being unavailable fails open.</b> A Redis outage must not become a
/// login outage, so a limiter that cannot reach Redis lets the request through
/// and says so in the log. The deliberate trade is that a sustained Redis
/// failure also means no throttling, which is why this covers only the
/// authentication endpoints rather than the whole API, and why readiness
/// still reports Redis as unhealthy instead of masking it.
/// </para>
/// </remarks>
public sealed class RateLimitingMiddleware
{
    private const string AuthPathPrefix = "/api/v1/auth";

    private readonly RequestDelegate _next;
    private readonly IConnectionMultiplexer _redis;
    private readonly RateLimitingOptions _options;
    private readonly ILogger<RateLimitingMiddleware> _logger;
    private readonly JsonSerializerOptions _jsonOptions;

    public RateLimitingMiddleware(
        RequestDelegate next,
        IConnectionMultiplexer redis,
        IOptions<RateLimitingOptions> options,
        IOptions<JsonOptions> mvcJsonOptions,
        ILogger<RateLimitingMiddleware> logger)
    {
        _next = next;
        _redis = redis;
        _options = options.Value;
        _jsonOptions = mvcJsonOptions.Value.JsonSerializerOptions;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path.Value ?? string.Empty;

        if (!IsAuthenticationPath(path))
        {
            await _next(context);

            return;
        }

        var limit = _options.AuthenticationPermitLimit;
        var window = _options.AuthenticationWindow;

        if (limit <= 0)
        {
            await _next(context);

            return;
        }

        var client = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var key = $"{_options.KeyPrefix}:auth:{client}";

        long attempts;

        try
        {
            attempts = await IncrementAsync(key, window);
        }
        catch (RedisException exception)
        {
            // Fail open, loudly. See the type remarks.
            _logger.LogWarning(
                exception,
                "Rate limiting is not being applied to {Path} because Redis is unavailable.",
                path);

            await _next(context);

            return;
        }

        if (attempts <= limit)
        {
            await _next(context);

            return;
        }

        var retryAfter = (int)Math.Ceiling(window.TotalSeconds);

        context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        context.Response.ContentType = "application/json";
        context.Response.Headers.RetryAfter = retryAfter.ToString(CultureInfo.InvariantCulture);

        // Counted against the limit rather than served, so a client that keeps
        // hammering cannot keep the window alive indefinitely.
        var response = new ApiErrorResponse
        {
            Error = new ApiError
            {
                Code = "RATE_LIMIT_EXCEEDED",
                Message =
                    "Too many authentication attempts. " +
                    $"Try again in {retryAfter} seconds."
            },
            TraceId = context.TraceIdentifier
        };

        await context.Response.WriteAsync(
            JsonSerializer.Serialize(response, _jsonOptions),
            context.RequestAborted);
    }

    private static bool IsAuthenticationPath(string path)
    {
        if (!path.StartsWith(
                AuthPathPrefix,
                StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        // Only this API's own auth routes. The slash check stops the prefix from
        // also matching an unrelated route that merely starts with the same text.
        return path.Length == AuthPathPrefix.Length ||
               path[AuthPathPrefix.Length] == '/';
    }

    private async Task<long> IncrementAsync(string key, TimeSpan window)
    {
        var database = _redis.GetDatabase();

        var attempts = await database.StringIncrementAsync(key);

        if (attempts == 1)
        {
            // Only the request that opened the window sets the expiry, so the
            // window is anchored to the first attempt and is not extended by
            // later traffic.
            await database.KeyExpireAsync(key, window);
        }

        return attempts;
    }
}
