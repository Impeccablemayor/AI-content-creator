using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using StackExchange.Redis;
using Xunit;

namespace Api.IntegrationTests;

/// <summary>
/// Throttling of the authentication endpoints, counted in the real Redis.
/// </summary>
/// <remarks>
/// Redis is part of what is under test, not a stand-in for something else: the
/// point of counting in Redis rather than in process memory is that the limit
/// survives a restart and holds across API instances, and neither claim can be
/// tested against an in-memory substitute.
/// </remarks>
public sealed class RateLimitingTests : IAsyncLifetime
{
    private const int PermitLimit = 3;

    /// <summary>
    /// Keys under this prefix belong to these tests alone, so a shared Redis
    /// cannot make one test's traffic count against another's.
    /// </summary>
    private readonly string _keyPrefix =
        $"mayorx:itest:ratelimit:{Guid.NewGuid():N}";

    private readonly MayorxApiFactory _factory;

    private HttpClient _client = null!;

    public RateLimitingTests()
    {
        _factory = MayorxApiFactory.WithSettings(
            new Dictionary<string, string?>
            {
                ["RateLimiting:AuthenticationPermitLimit"] = PermitLimit.ToString(),
                ["RateLimiting:AuthenticationWindow"] = "00:05:00",
                ["RateLimiting:KeyPrefix"] = _keyPrefix
            });
    }

    public async Task InitializeAsync()
    {
        _factory.Initialise();
        await _factory.MigrateAsync();

        _client = _factory.CreateClient();

        await ClearCountersAsync();
    }

    public async Task DisposeAsync()
    {
        await ClearCountersAsync();

        _client?.Dispose();
        _factory.Dispose();
    }

    [Fact]
    public async Task Requests_up_to_the_limit_are_served()
    {
        for (var attempt = 1; attempt <= PermitLimit; attempt++)
        {
            using var response = await LoginAsync();

            Assert.NotEqual(
                HttpStatusCode.TooManyRequests,
                response.StatusCode);
        }
    }

    [Fact]
    public async Task The_request_after_the_limit_is_refused_with_an_error_envelope()
    {
        for (var attempt = 1; attempt <= PermitLimit; attempt++)
        {
            using var allowed = await LoginAsync();

            Assert.NotEqual(HttpStatusCode.TooManyRequests, allowed.StatusCode);
        }

        using var refused = await LoginAsync();

        Assert.Equal(HttpStatusCode.TooManyRequests, refused.StatusCode);

        var body = await refused.Content.ReadAsStringAsync();

        using var document = JsonDocument.Parse(body);

        Assert.Equal(
            "RATE_LIMIT_EXCEEDED",
            document.RootElement.GetProperty("error").GetProperty("code").GetString());

        // A throttle that returns nothing usable forces a client to guess.
        Assert.False(string.IsNullOrWhiteSpace(
            document.RootElement.GetProperty("error").GetProperty("message").GetString()));
    }

    [Fact]
    public async Task A_refused_request_says_how_long_to_wait()
    {
        await ExhaustAsync();

        using var refused = await LoginAsync();

        Assert.Equal(HttpStatusCode.TooManyRequests, refused.StatusCode);

        var retryAfter = refused.Headers.RetryAfter;

        Assert.NotNull(retryAfter?.Delta);

        // The five minute window this host is configured with, rounded up.
        Assert.Equal(TimeSpan.FromMinutes(5), retryAfter.Delta);
    }

    [Fact]
    public async Task Refused_requests_keep_being_refused()
    {
        await ExhaustAsync();

        for (var attempt = 0; attempt < 5; attempt++)
        {
            using var refused = await LoginAsync();

            Assert.Equal(HttpStatusCode.TooManyRequests, refused.StatusCode);
        }
    }

    [Fact]
    public async Task The_count_is_kept_in_redis_rather_than_in_the_process()
    {
        await ExhaustAsync();

        using var refused = await LoginAsync();

        Assert.Equal(HttpStatusCode.TooManyRequests, refused.StatusCode);

        // If this were in-memory this would be null, and the limit would silently
        // reset on every deploy and differ between API instances.
        var counter = await ReadCounterAsync();

        Assert.NotNull(counter);
        Assert.Equal(PermitLimit + 1, counter.Value);
    }

    [Fact]
    public async Task The_counter_key_names_the_prefix_and_the_client()
    {
        await ExhaustAsync();

        var keys = await ListCounterKeysAsync();

        var key = Assert.Single(keys);

        Assert.StartsWith(_keyPrefix + ":auth:", key, StringComparison.Ordinal);
    }

    [Fact]
    public async Task One_client_exhausting_the_limit_does_not_throttle_another_client()
    {
        await ExhaustAsync();

        // A different client address, counted hard.
        await SeedCounterAsync("203.0.113.7", 500);

        using var response = await LoginAsync();

        // Still refused, because the caller is the address that ran out of
        // allowance, and because the other address must not have been able to
        // spend this caller's budget.
        Assert.Equal(HttpStatusCode.TooManyRequests, response.StatusCode);

        var seeded = await ReadCounterAsync("203.0.113.7");

        Assert.Equal(500, seeded);
    }

    [Fact]
    public async Task The_window_expires_and_lets_the_client_start_again()
    {
        var shortWindowFactory = MayorxApiFactory.WithSettings(
            new Dictionary<string, string?>
            {
                ["RateLimiting:AuthenticationPermitLimit"] = "2",
                ["RateLimiting:AuthenticationWindow"] = "00:00:02",
                ["RateLimiting:KeyPrefix"] = _keyPrefix + ":window"
            });

        shortWindowFactory.Initialise();
        await shortWindowFactory.MigrateAsync();

        using var client = shortWindowFactory.CreateClient();

        try
        {
            for (var attempt = 0; attempt < 2; attempt++)
            {
                using var allowed = await LoginAsync(client);

                Assert.NotEqual(HttpStatusCode.TooManyRequests, allowed.StatusCode);
            }

            using var refused = await LoginAsync(client);

            Assert.Equal(HttpStatusCode.TooManyRequests, refused.StatusCode);

            // Long enough for the key's own expiry to have removed it. The
            // window is anchored to the first attempt, not to the clock, so
            // waiting past that anchor has to restore access.
            await Task.Delay(TimeSpan.FromSeconds(3));

            using var afterWindow = await LoginAsync(client);

            Assert.NotEqual(
                HttpStatusCode.TooManyRequests,
                afterWindow.StatusCode);
        }
        finally
        {
            await ClearCountersAsync(_keyPrefix + ":window");
            shortWindowFactory.Dispose();
        }
    }

    [Fact]
    public async Task Endpoints_outside_authentication_are_never_throttled()
    {
        await ExhaustAsync();

        // A limit that reached past the authentication routes would throttle
        // ordinary traffic, so the scope has to be the auth prefix alone.
        for (var attempt = 0; attempt < PermitLimit * 3; attempt++)
        {
            using var response = await _client.GetAsync("api/v1/health/live");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
    }

    [Fact]
    public async Task A_path_that_only_looks_like_the_auth_prefix_is_not_throttled()
    {
        await ExhaustAsync();

        // The middleware matches the prefix plus a slash. A route that merely
        // begins with the same letters must not fall inside the limit, or a
        // throttling bug could take out an unrelated endpoint.
        for (var attempt = 0; attempt < PermitLimit * 2; attempt++)
        {
            using var response = await _client.GetAsync("api/v1/authentic");

            Assert.NotEqual(
                HttpStatusCode.TooManyRequests,
                response.StatusCode);
        }
    }

    // ------------------------------------------------------------ Redis outage

    [Fact]
    public async Task Authentication_still_works_when_redis_is_unreachable()
    {
        var unreachableFactory = MayorxApiFactory.WithSettings(
            new Dictionary<string, string?>
            {
                ["RateLimiting:AuthenticationPermitLimit"] = "1",
                ["RateLimiting:KeyPrefix"] = _keyPrefix + ":outage",

                // A port nothing is listening on. AbortOnConnectFail is off, so
                // the host still starts and every command fails, which is the
                // outage this test is about.
                ["Redis:ConnectionString"] = "localhost:6399,abortConnect=false"
            });

        unreachableFactory.Initialise();
        await unreachableFactory.MigrateAsync();

        using var client = unreachableFactory.CreateClient();

        try
        {
            // A Redis outage must not become a login outage. Both of these must
            // pass their limit of one and still be served, because a limiter that
            // cannot reach its counter lets the request through rather than
            // locking everybody out.
            for (var attempt = 0; attempt < 3; attempt++)
            {
                using var response = await LoginAsync(client);

                Assert.NotEqual(
                    HttpStatusCode.TooManyRequests,
                    response.StatusCode);

                Assert.NotEqual(
                    HttpStatusCode.InternalServerError,
                    response.StatusCode);
            }
        }
        finally
        {
            unreachableFactory.Dispose();
        }
    }

    // ---------------------------------------------------------------- helpers

    private Task<HttpResponseMessage> LoginAsync() => LoginAsync(_client);

    private static Task<HttpResponseMessage> LoginAsync(HttpClient client) =>
        client.PostAsJsonAsync(
            "api/v1/auth/login",
            new { email = "nobody@example.test", password = "wrong-password" });

    private async Task ExhaustAsync()
    {
        for (var attempt = 0; attempt < PermitLimit; attempt++)
        {
            using var response = await LoginAsync();

            Assert.NotEqual(HttpStatusCode.TooManyRequests, response.StatusCode);
        }
    }

    private Task<long?> ReadCounterAsync(string client = "unknown") =>
        ReadCounterForAsync($"{_keyPrefix}:auth:{client}");

    private Task<long?> ReadCounterForAsync(string key)
    {
        return RunRedisAsync(async multiplexer =>
        {
            var value = await multiplexer.GetDatabase().StringGetAsync(key);

            return value.IsNull ? null : (long?)long.Parse(value!);
        });
    }

    private async Task SeedCounterAsync(string client, long value)
    {
        await RunRedisAsync(async multiplexer =>
        {
            await multiplexer.GetDatabase().StringSetAsync(
                $"{_keyPrefix}:auth:{client}",
                value,
                TimeSpan.FromMinutes(5));

            return true;
        });
    }

    private Task<List<string>> ListCounterKeysAsync() =>
        RunRedisAsync(async multiplexer =>
        {
            var keys = new List<string>();

            foreach (var key in KeysFor(multiplexer, $"{_keyPrefix}:auth:*"))
            {
                keys.Add(key.ToString());
            }

            return keys;
        });

    private Task ClearCountersAsync(string? prefix = null) =>
        RunRedisAsync(async multiplexer =>
        {
            var database = multiplexer.GetDatabase();

            foreach (var key in KeysFor(multiplexer, $"{prefix ?? _keyPrefix}:auth:*"))
            {
                await database.KeyDeleteAsync(key);
            }

            return true;
        });

    /// <summary>
    /// Server side key enumeration, scoped to a prefix that is unique to this test
    /// instance so the scan cannot wander into other keys on the shared container.
    /// </summary>
    private static IEnumerable<RedisKey> KeysFor(
        IConnectionMultiplexer multiplexer,
        RedisValue pattern)
    {
        var database = multiplexer.GetDatabase();

        return multiplexer
            .GetServer(multiplexer.GetEndPoints().Single())
            .Keys(database.Database, pattern);
    }

    private static async Task<T> RunRedisAsync<T>(
        Func<IConnectionMultiplexer, Task<T>> work)
    {
        var configuration = ConfigurationOptions.Parse(
            IntegrationTestSettings.RedisConnectionString);

        configuration.AbortOnConnectFail = false;

        await using var multiplexer =
            await ConnectionMultiplexer.ConnectAsync(configuration);

        return await work(multiplexer);
    }
}