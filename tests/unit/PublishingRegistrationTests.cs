using Api.Application.Publishing;
using Api.Application.Scheduling;
using Api.Application.Security;
using Api.Application.SocialAccounts;
using Api.Configuration;
using Api.Domain.Enums;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Xunit;

namespace Api.Tests;

/// <summary>
/// The worker has no HTTP surface, so a registration mistake in its publishing
/// graph would otherwise only appear when a post is due.
/// </summary>
public sealed class PublishingRegistrationTests
{
    [Fact]
    public void The_publish_service_and_its_dependencies_resolve()
    {
        var provider = BuildProvider();

        using var scope = provider.CreateScope();

        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IScheduledPostPublishService>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<ISocialCredentialProtector>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<ISocialPlatformPublisherResolver>());
    }

    [Fact]
    public void Both_platforms_are_registered_with_their_own_configured_endpoints()
    {
        var provider = BuildProvider();

        using var scope = provider.CreateScope();
        var resolver = scope.ServiceProvider.GetRequiredService<ISocialPlatformPublisherResolver>();
        var linkedIn = scope.ServiceProvider
            .GetRequiredService<IOptions<LinkedInOptions>>().Value;
        var x = scope.ServiceProvider.GetRequiredService<IOptions<XOptions>>().Value;

        Assert.Equal(ContentPlatform.LinkedIn, resolver.Get(ContentPlatform.LinkedIn).Platform);
        Assert.Equal(ContentPlatform.X, resolver.Get(ContentPlatform.X).Platform);
        Assert.Equal("https://linkedin.example.com/v2/ugcPosts", linkedIn.PublishEndpoint);
        Assert.Equal("https://x.example.com/2/tweets", x.PublishEndpoint);
    }

    [Fact]
    public void Publishing_scopes_are_configured_for_each_platform()
    {
        var provider = BuildProvider();

        using var scope = provider.CreateScope();
        var linkedIn = scope.ServiceProvider
            .GetRequiredService<IOptions<LinkedInOptions>>().Value;
        var x = scope.ServiceProvider.GetRequiredService<IOptions<XOptions>>().Value;

        Assert.Contains("w_member_social", linkedIn.Scopes);
        Assert.Contains("tweet.write", x.Scopes);
    }

    /// <summary>
    /// A publish endpoint is bound from configuration. A typo would otherwise
    /// surface only as a failed publish, long after startup.
    /// </summary>
    [Fact]
    public void A_non_https_publish_endpoint_fails_validation()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSocialPublishing(CreateConfiguration(publishEndpoint: "http://insecure.example.com/posts"));

        using var provider = services.BuildServiceProvider();

        var exception = Assert.Throws<OptionsValidationException>(() =>
            provider.GetRequiredService<IOptions<LinkedInOptions>>().Value);
        Assert.Contains("absolute HTTPS endpoints", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void An_unconfigured_platform_may_omit_its_publish_endpoint()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSocialPublishing(new ConfigurationBuilder().Build());

        using var provider = services.BuildServiceProvider();

        var linkedIn = provider.GetRequiredService<IOptions<LinkedInOptions>>().Value;
        Assert.False(linkedIn.IsFullyConfigured);
    }

    private static ServiceProvider BuildProvider()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IHostEnvironment>(new TestHostEnvironment());
        services.AddSocialCredentialProtection(CreateConfiguration(), new TestHostEnvironment());
        services.AddSocialPublishing(CreateConfiguration());

        // The store is supplied by the worker's composition root, not by
        // AddSocialPublishing, so this test supplies its own. Resolving the
        // publish service proves the publishing graph itself is complete.
        services.AddScoped<IScheduledPostStore, UnusedScheduledPostStore>();

        return services.BuildServiceProvider();
    }

    private sealed class UnusedScheduledPostStore : IScheduledPostStore
    {
        public Task<IReadOnlyList<ScheduledPostCandidate>> FindDueAsync(
            DateTime now, int limit, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<bool> TryClaimAsync(
            Guid contentPostId, DateTime now, TimeSpan claimExpiry, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<bool> TryClaimForImmediatePublishAsync(
            Guid contentPostId, Guid workspaceId, DateTime now, TimeSpan claimExpiry, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<bool> TryCancelScheduleAsync(
            Guid contentPostId, Guid workspaceId, DateTime now, TimeSpan claimExpiry, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<bool> TryScheduleAsync(
            Guid contentPostId, Guid workspaceId, DateTime scheduledAt, DateTime now, TimeSpan claimExpiry, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<ScheduledPostPayload?> GetPublishPayloadAsync(
            Guid contentPostId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<PublishAccountCandidate>> FindPublishAccountsAsync(
            Guid workspaceId, ContentPlatform platform, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<bool> TryMarkPublishedAsync(
            Guid contentPostId, DateTime claimedAt, DateTime publishedAt, string? externalPostId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<bool> TryRecordPublishFailureAsync(
            Guid contentPostId, DateTime claimedAt, PublishFailureReason reason, DateTime failedAt, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    private static IConfiguration CreateConfiguration(
        string publishEndpoint = "https://linkedin.example.com/v2/ugcPosts") =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["DataProtectionKeyRing:ApplicationName"] = "Mayorx.ContentEngine.Tests",
                ["Social:LinkedIn:ClientId"] = "linkedin-client",
                ["Social:LinkedIn:ClientSecret"] = "unit-test-placeholder",
                ["Social:LinkedIn:RedirectUri"] = "https://app.example.com/api/v1/social/linkedin/callback",
                ["Social:LinkedIn:Scopes:0"] = "openid",
                ["Social:LinkedIn:Scopes:1"] = "w_member_social",
                ["Social:LinkedIn:PublishEndpoint"] = publishEndpoint,
                ["Social:X:ClientId"] = "x-client",
                ["Social:X:ClientSecret"] = "unit-test-placeholder",
                ["Social:X:RedirectUri"] = "https://app.example.com/api/v1/social/x/callback",
                ["Social:X:Scopes:0"] = "tweet.read",
                ["Social:X:Scopes:1"] = "tweet.write",
                ["Social:X:PublishEndpoint"] = "https://x.example.com/2/tweets"
            })
            .Build();

    private sealed class TestHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Development";
        public string ApplicationName { get; set; } = "Api.Tests";
        public string ContentRootPath { get; set; } = Directory.GetCurrentDirectory();
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
