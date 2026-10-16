using Api.Application.SocialAccounts;
using Api.Configuration;
using Api.Domain.Enums;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Api.Tests;

public sealed class SocialPlatformProviderRegistrationTests
{
    [Fact]
    public void The_provider_resolver_loads_both_platforms_without_credentials()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddConfiguredSocialPlatformProviders(new ConfigurationBuilder().Build());

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var resolver = scope.ServiceProvider.GetRequiredService<ISocialPlatformProviderResolver>();

        var linkedIn = resolver.Get(ContentPlatform.LinkedIn);
        var x = resolver.Get(ContentPlatform.X);

        Assert.IsType<LinkedInSocialPlatformProvider>(linkedIn);
        Assert.IsType<XSocialPlatformProvider>(x);
        Assert.False(linkedIn.IsConfigured);
        Assert.False(x.IsConfigured);
    }
}
