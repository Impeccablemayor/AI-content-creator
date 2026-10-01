using Api.Configuration;
using Api.Infrastructure.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Api.Application.AnalyticsSync;

public static class AnalyticsSyncRegistration
{
    /// <summary>
    /// Registers the metrics sync pipeline. Call after
    /// <see cref="Publishing.PublishingRegistration.AddSocialPublishing"/>,
    /// which owns the shared social platform client configuration.
    /// </summary>
    public static IServiceCollection AddAnalyticsSync(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddOptions<AnalyticsSyncOptions>()
            .Bind(configuration.GetSection(AnalyticsSyncOptions.SectionName))
            .Validate(
                options => options.PollIntervalSeconds is >= 1 and <= 3600,
                "Analytics sync poll interval must be between 1 and 3600 seconds.")
            .Validate(
                options => options.BatchSize is >= 1 and <= 100,
                "Analytics sync batch size must be between 1 and 100.")
            .Validate(
                options => options.ResyncIntervalHours is >= 1 and <= 720,
                "Analytics sync resync interval must be between 1 and 720 hours.")
            .ValidateOnStart();

        // The client timeout and IPv4 preference are shared with publishing, so
        // the metrics calls behave the same as the calls that published the post.
        services.AddHttpClient<LinkedInSocialPlatformMetricsProvider>((serviceProvider, client) =>
                ConfigureClient(
                    client,
                    serviceProvider.GetRequiredService<IOptions<LinkedInOptions>>().Value))
            .ConfigurePrimaryHttpMessageHandler(Ipv4PreferredHttpConnection.CreateHandler);

        services.AddHttpClient<XSocialPlatformMetricsProvider>((serviceProvider, client) =>
                ConfigureClient(
                    client,
                    serviceProvider.GetRequiredService<IOptions<XOptions>>().Value))
            .ConfigurePrimaryHttpMessageHandler(Ipv4PreferredHttpConnection.CreateHandler);

        services.AddScoped<ISocialPlatformMetricsProvider, LinkedInSocialPlatformMetricsProvider>();
        services.AddScoped<ISocialPlatformMetricsProvider, XSocialPlatformMetricsProvider>();

        services.AddScoped<ISocialPlatformMetricsProviderResolver, SocialPlatformMetricsProviderResolver>();
        services.AddScoped<IPublishedPostMetricsSyncService, PublishedPostMetricsSyncService>();
        services.AddScoped<IAnalyticsSyncProcessor, AnalyticsSyncProcessor>();

        return services;
    }

    private static void ConfigureClient(
        HttpClient client,
        SocialPlatformClientOptions options) =>
        client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
}
