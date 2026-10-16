using Api.Application.SocialAccounts;
using Api.Configuration;
using Api.Contracts.Social;
using Api.Infrastructure.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Api.Application.SocialAccounts;

public static class SocialPlatformProviderRegistration
{
    public static IServiceCollection AddConfiguredSocialPlatformProviders(
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

        services.AddSingleton(serviceProvider =>
            serviceProvider.GetRequiredService<IOptions<LinkedInOptions>>().Value);
        services.AddSingleton(serviceProvider =>
            serviceProvider.GetRequiredService<IOptions<XOptions>>().Value);

        services.AddHttpClient<LinkedInSocialPlatformProvider>((serviceProvider, client) =>
                ConfigureClient(
                    client,
                    serviceProvider.GetRequiredService<IOptions<LinkedInOptions>>().Value))
            .ConfigurePrimaryHttpMessageHandler(Ipv4PreferredHttpConnection.CreateHandler);

        services.AddHttpClient<XSocialPlatformProvider>((serviceProvider, client) =>
                ConfigureClient(
                    client,
                    serviceProvider.GetRequiredService<IOptions<XOptions>>().Value))
            .ConfigurePrimaryHttpMessageHandler(Ipv4PreferredHttpConnection.CreateHandler);

        services.AddScoped<ISocialPlatformProvider>(serviceProvider =>
            serviceProvider.GetRequiredService<LinkedInSocialPlatformProvider>());
        services.AddScoped<ISocialPlatformProvider>(serviceProvider =>
            serviceProvider.GetRequiredService<XSocialPlatformProvider>());

        services.AddScoped<ISocialPlatformProviderResolver, SocialPlatformProviderResolver>();
        services.AddScoped<ISocialOAuthStateStore, DistributedSocialOAuthStateStore>();
        services.AddScoped<ISocialAccountService, SocialAccountService>();

        return services;
    }

    private static void ConfigureClient(
        HttpClient client,
        SocialPlatformClientOptions options)
    {
        client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
    }
}
