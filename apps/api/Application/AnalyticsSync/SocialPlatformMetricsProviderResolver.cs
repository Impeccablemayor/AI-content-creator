using Api.Application.SocialAccounts;
using Api.Domain.Enums;

namespace Api.Application.AnalyticsSync;

public sealed class SocialPlatformMetricsProviderResolver(
    IEnumerable<ISocialPlatformMetricsProvider> providers) : ISocialPlatformMetricsProviderResolver
{
    public ISocialPlatformMetricsProvider Get(ContentPlatform platform)
    {
        var provider = providers.FirstOrDefault(candidate => candidate.Platform == platform);
        if (provider is null)
        {
            throw new UnsupportedSocialPlatformException(
                $"Post metrics are not available for platform {platform}.");
        }

        return provider;
    }
}
