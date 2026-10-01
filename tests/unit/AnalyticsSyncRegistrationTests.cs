using Api.Application.AnalyticsSync;
using Api.Application.Publishing;
using Api.Application.Security;
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
/// The worker has no HTTP surface, so a registration mistake in its metrics sync
/// graph would otherwise only appear once a post is due for measurement.
/// </summary>
public sealed class AnalyticsSyncRegistrationTests
{
    [Fact]
    public void The_sync_service_and_its_dependencies_resolve()
    {
        var provider = BuildProvider();

        using var scope = provider.CreateScope();

        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IAnalyticsSyncProcessor>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IPublishedPostMetricsSyncService>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<ISocialCredentialProtector>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<ISocialPlatformMetricsProviderResolver>());
    }

    [Fact]
    public void Both_platforms_are_registered_with_their_own_configured_metrics_endpoints()
    {
        var provider = BuildProvider();

        using var scope = provider.CreateScope();
        var resolver = scope.ServiceProvider
            .GetRequiredService<ISocialPlatformMetricsProviderResolver>();
        var linkedIn = scope.ServiceProvider
            .GetRequiredService<IOptions<LinkedInOptions>>().Value;
        var x = scope.ServiceProvider.GetRequiredService<IOptions<XOptions>>().Value;

        Assert.Equal(ContentPlatform.LinkedIn, resolver.Get(ContentPlatform.LinkedIn).Platform);
        Assert.Equal(ContentPlatform.X, resolver.Get(ContentPlatform.X).Platform);
        Assert.Equal("https://linkedin.example.com/rest/memberCreatorPostAnalytics", linkedIn.MetricsEndpoint);
        Assert.Equal("https://x.example.com/2/tweets", x.MetricsEndpoint);
        Assert.Equal("202601", linkedIn.MetricsVersion);
    }

    [Fact]
    public void The_default_resync_interval_spaces_out_repeated_measurements()
    {
        var provider = BuildProvider();

        var options = provider.GetRequiredService<IOptions<AnalyticsSyncOptions>>().Value;

        Assert.Equal(24, options.ResyncIntervalHours);
        Assert.Equal(10, options.BatchSize);
    }

    /// <summary>
    /// A metrics endpoint is bound from configuration. A typo would otherwise
    /// surface only as a failed measurement, long after startup.
    /// </summary>
    [Fact]
    public void A_non_https_metrics_endpoint_fails_validation()
    {
        // The metrics endpoint is a social platform option, so it has to fail
        // through the same validation the publishing registration relies on.
        var configuration = CreateConfiguration(
            metricsEndpoint: "http://insecure.example.com/analytics");

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSocialPublishing(configuration);
        services.AddAnalyticsSync(configuration);

        using var provider = services.BuildServiceProvider();

        var exception = Assert.Throws<OptionsValidationException>(() =>
            provider.GetRequiredService<IOptions<LinkedInOptions>>().Value);
        Assert.Contains("absolute HTTPS endpoints", exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("AnalyticsSync:PollIntervalSeconds", "0")]
    [InlineData("AnalyticsSync:BatchSize", "0")]
    [InlineData("AnalyticsSync:ResyncIntervalHours", "0")]
    public void An_out_of_range_setting_fails_validation(string key, string value)
    {
        var settings = CreateConfiguration().AsEnumerable()
            .ToDictionary(entry => entry.Key, entry => entry.Value);
        settings[key] = value;

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAnalyticsSync(new ConfigurationBuilder()
            .AddInMemoryCollection(settings)
            .Build());

        using var provider = services.BuildServiceProvider();

        Assert.Throws<OptionsValidationException>(() =>
            provider.GetRequiredService<IOptions<AnalyticsSyncOptions>>().Value);
    }

    private static ServiceProvider BuildProvider()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IHostEnvironment>(new TestHostEnvironment());
        services.AddSocialCredentialProtection(CreateConfiguration(), new TestHostEnvironment());
        services.AddSocialPublishing(CreateConfiguration());
        services.AddAnalyticsSync(CreateConfiguration());

        // The store is supplied by the worker's composition root, not by
        // AddAnalyticsSync, so this test supplies its own. Resolving the processor
        // proves the sync graph itself is complete.
        services.AddScoped<IAnalyticsSyncStore, UnusedAnalyticsSyncStore>();

        return services.BuildServiceProvider();
    }

    private sealed class UnusedAnalyticsSyncStore : IAnalyticsSyncStore
    {
        public Task<IReadOnlyList<MetricsSyncCandidate>> FindDueAsync(
            DateTime dueBefore, int limit, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<MetricsSyncAccountCandidate>> FindAccountsAsync(
            Guid workspaceId, ContentPlatform platform, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<bool> TryRecordAttemptAsync(
            Guid contentPostId, DateTime attemptedAt, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task AddSnapshotAsync(
            ContentPostMetricSnapshot snapshot, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    private static IConfiguration CreateConfiguration(
        string metricsEndpoint = "https://linkedin.example.com/rest/memberCreatorPostAnalytics") =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["DataProtectionKeyRing:ApplicationName"] = "Mayorx.ContentEngine.Tests",
                ["Social:LinkedIn:ClientId"] = "linkedin-client",
                ["Social:LinkedIn:ClientSecret"] = "unit-test-placeholder",
                ["Social:LinkedIn:RedirectUri"] = "https://app.example.com/api/v1/social/linkedin/callback",
                ["Social:LinkedIn:Scopes:0"] = "openid",
                ["Social:LinkedIn:Scopes:1"] = "w_member_social",
                ["Social:LinkedIn:PublishEndpoint"] = "https://linkedin.example.com/v2/ugcPosts",
                ["Social:LinkedIn:MetricsEndpoint"] = metricsEndpoint,
                ["Social:X:ClientId"] = "x-client",
                ["Social:X:ClientSecret"] = "unit-test-placeholder",
                ["Social:X:RedirectUri"] = "https://app.example.com/api/v1/social/x/callback",
                ["Social:X:Scopes:0"] = "tweet.read",
                ["Social:X:Scopes:1"] = "tweet.write",
                ["Social:X:PublishEndpoint"] = "https://x.example.com/2/tweets",
                ["Social:X:MetricsEndpoint"] = "https://x.example.com/2/tweets"
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
