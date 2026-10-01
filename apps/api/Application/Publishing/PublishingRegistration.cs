using Api.Application.Scheduling;
using Api.Application.SocialAccounts;
using Api.Configuration;
using Api.Infrastructure.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Api.Application.Publishing;

public static class PublishingRegistration
{
    /// <summary>
    /// Registers publishing. The worker calls this instead of the API's
    /// connection registration, because a worker never accepts an OAuth callback
    /// and has no state store to keep.
    /// </summary>
    public static IServiceCollection AddSocialPublishing(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddOptions<SocialPlatformOptions>()
            .Bind(configuration.GetSection(SocialPlatformOptions.SectionName))
            .Validate(
                options => options.StateLifetimeMinutes is >= 1 and <= 60,
                "Social OAuth state lifetime must be between 1 and 60 minutes.")
            .ValidateOnStart();

        services.AddOptions<LinkedInOptions>()
            .Bind(configuration.GetSection(LinkedInOptions.SectionName))
            .Validate(
                SocialPlatformProviderRegistrationRules.IsValidLinkedIn,
                "LinkedIn OAuth configuration is incomplete or invalid. Provide ClientId, ClientSecret, a valid RedirectUri, at least one scope, and absolute HTTPS endpoints, or leave the platform unconfigured.")
            .ValidateOnStart();

        services.AddOptions<XOptions>()
            .Bind(configuration.GetSection(XOptions.SectionName))
            .Validate(
                SocialPlatformProviderRegistrationRules.IsValidX,
                "X OAuth configuration is incomplete or invalid. Provide ClientId, ClientSecret, a valid RedirectUri, at least one scope, and absolute HTTPS endpoints, or leave the platform unconfigured.")
            .ValidateOnStart();

        services.AddHttpClient<LinkedInSocialPlatformPublisher>((serviceProvider, client) =>
                ConfigureClient(
                    client,
                    serviceProvider.GetRequiredService<IOptions<LinkedInOptions>>().Value))
            .ConfigurePrimaryHttpMessageHandler(Ipv4PreferredHttpConnection.CreateHandler);

        services.AddHttpClient<XSocialPlatformPublisher>((serviceProvider, client) =>
                ConfigureClient(
                    client,
                    serviceProvider.GetRequiredService<IOptions<XOptions>>().Value))
            .ConfigurePrimaryHttpMessageHandler(Ipv4PreferredHttpConnection.CreateHandler);

        services.AddScoped<ISocialPlatformPublisher, LinkedInSocialPlatformPublisher>();
        services.AddScoped<ISocialPlatformPublisher, XSocialPlatformPublisher>();

        services.AddScoped<ISocialPlatformPublisherResolver, SocialPlatformPublisherResolver>();
        services.AddScoped<IScheduledPostPublishService, ScheduledPostPublishService>();

        return services;
    }

    private static void ConfigureClient(
        HttpClient client,
        SocialPlatformClientOptions options) =>
        client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
}
