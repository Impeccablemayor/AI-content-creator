using System.Text.Json;
using Api.Application.SocialAccounts;
using Api.Configuration;
using Api.Domain.Enums;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Xunit;

namespace Api.Tests;

public sealed class SocialOAuthStateStoreTests
{
    [Fact]
    public async Task A_stored_state_round_trips_its_workspace_platform_and_verifier()
    {
        var store = CreateStore();
        var workspaceId = Guid.NewGuid();

        await store.StoreAsync(
            "state-value",
            new SocialOAuthState(workspaceId, ContentPlatform.X, "verifier-value"),
            CancellationToken.None);

        var consumed = await store.ConsumeAsync("state-value", CancellationToken.None);

        Assert.NotNull(consumed);
        Assert.Equal(workspaceId, consumed!.WorkspaceId);
        Assert.Equal(ContentPlatform.X, consumed.Platform);
        Assert.Equal("verifier-value", consumed.CodeVerifier);
    }

    [Fact]
    public async Task A_state_can_only_be_used_once()
    {
        var store = CreateStore();
        await store.StoreAsync(
            "state-value",
            new SocialOAuthState(Guid.NewGuid(), ContentPlatform.LinkedIn, null),
            CancellationToken.None);

        var first = await store.ConsumeAsync("state-value", CancellationToken.None);
        var replay = await store.ConsumeAsync("state-value", CancellationToken.None);

        Assert.NotNull(first);
        Assert.Null(replay);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public async Task A_missing_or_blank_state_never_resolves(string? state)
    {
        var store = CreateStore();

        Assert.Null(await store.ConsumeAsync(state!, CancellationToken.None));
    }

    [Fact]
    public async Task An_unknown_state_never_resolves()
    {
        var store = CreateStore();

        Assert.Null(await store.ConsumeAsync("never-stored", CancellationToken.None));
    }

    /// <summary>
    /// The state must expire on its own: an abandoned authorization cannot be
    /// replayed later, so the cache entry carries the configured lifetime.
    /// </summary>
    [Fact]
    public async Task The_configured_state_lifetime_is_applied_to_the_stored_value()
    {
        var cache = new RecordingCache();
        var store = CreateStore(cache, stateLifetimeMinutes: 7);

        await store.StoreAsync(
            "state-value",
            new SocialOAuthState(Guid.NewGuid(), ContentPlatform.X, "verifier"),
            CancellationToken.None);

        Assert.Equal(
            TimeSpan.FromMinutes(7),
            cache.LastSetOptions!.AbsoluteExpirationRelativeToNow);
    }

    [Fact]
    public async Task The_cached_state_never_contains_a_credential()
    {
        var cache = new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions { SizeLimit = 1024 }));
        var store = CreateStore(cache);

        await store.StoreAsync(
            "state-value",
            new SocialOAuthState(Guid.NewGuid(), ContentPlatform.X, "verifier"),
            CancellationToken.None);

        var payload = await cache.GetStringAsync("social:oauth-state:state-value");

        Assert.NotNull(payload);
        Assert.DoesNotContain("access_token", payload!, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("refresh_token", payload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("client_secret", payload, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task A_corrupted_state_payload_is_treated_as_missing()
    {
        var cache = new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions { SizeLimit = 1024 }));
        var store = CreateStore(cache);
        await cache.SetStringAsync("social:oauth-state:state-value", "{ not json");

        Assert.Null(await store.ConsumeAsync("state-value", CancellationToken.None));
    }

    [Fact]
    public void LinkedIn_states_carry_no_pkce_verifier_but_x_states_do()
    {
        var (linkedInState, linkedInVerifier) = SocialOAuthStateFactory.Create(ContentPlatform.LinkedIn);
        var (xState, xVerifier) = SocialOAuthStateFactory.Create(ContentPlatform.X);

        Assert.False(string.IsNullOrWhiteSpace(linkedInState));
        Assert.Null(linkedInVerifier);
        Assert.False(string.IsNullOrWhiteSpace(xState));
        Assert.False(string.IsNullOrWhiteSpace(xVerifier));
    }

    [Fact]
    public void Generated_states_are_unique_and_url_safe()
    {
        var states = Enumerable
            .Range(0, 50)
            .Select(_ => SocialOAuthStateFactory.Create(ContentPlatform.X).State)
            .ToList();

        Assert.Equal(states.Count, states.Distinct().Count());
        Assert.All(states, state => Assert.Matches("^[A-Za-z0-9_-]+$", state));
    }

    private static DistributedSocialOAuthStateStore CreateStore(
        IDistributedCache? cache = null,
        int stateLifetimeMinutes = 10)
    {
        return new DistributedSocialOAuthStateStore(
            cache ?? new MemoryDistributedCache(
                Options.Create(new MemoryDistributedCacheOptions { SizeLimit = 1024 })),
            Options.Create(new SocialPlatformOptions
            {
                StateLifetimeMinutes = stateLifetimeMinutes
            }));
    }

    /// <summary>
    /// Records the expiration the store asks for, which a real cache would only
    /// apply over time.
    /// </summary>
    private sealed class RecordingCache : IDistributedCache
    {
        public DistributedCacheEntryOptions? LastSetOptions { get; private set; }

        public byte[]? Get(string key) => throw new NotSupportedException();

        public Task<byte[]?> GetAsync(string key, CancellationToken token = default) =>
            Task.FromResult<byte[]?>(null);

        public void Refresh(string key) => throw new NotSupportedException();

        public Task RefreshAsync(string key, CancellationToken token = default) =>
            throw new NotSupportedException();

        public void Remove(string key) => throw new NotSupportedException();

        public Task RemoveAsync(string key, CancellationToken token = default) =>
            throw new NotSupportedException();

        public void Set(string key, byte[] value, DistributedCacheEntryOptions options) =>
            throw new NotSupportedException();

        public Task SetAsync(
            string key,
            byte[] value,
            DistributedCacheEntryOptions options,
            CancellationToken token = default)
        {
            LastSetOptions = options;

            return Task.CompletedTask;
        }
    }
}
