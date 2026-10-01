using Api.Domain.Enums;

namespace Api.Application.Publishing;

public sealed class SocialPlatformPublisherResolver(
    IEnumerable<ISocialPlatformPublisher> publishers) : ISocialPlatformPublisherResolver
{
    public ISocialPlatformPublisher Get(ContentPlatform platform)
    {
        var publisher = publishers.FirstOrDefault(candidate => candidate.Platform == platform);
        if (publisher is null)
        {
            throw new SocialAccounts.UnsupportedSocialPlatformException(
                $"Publishing is not supported for platform {platform}.");
        }

        return publisher;
    }
}
