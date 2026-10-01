using Api.Domain.Enums;

namespace Api.Application.SocialAccounts;

/// <summary>
/// Resolves a provider for a platform. Only platforms with a registered
/// provider can be connected.
/// </summary>
public sealed class SocialPlatformProviderResolver : ISocialPlatformProviderResolver
{
    private readonly IReadOnlyDictionary<ContentPlatform, ISocialPlatformProvider> _providers;

    public SocialPlatformProviderResolver(IEnumerable<ISocialPlatformProvider> providers)
    {
        _providers = providers.ToDictionary(provider => provider.Platform);
    }

    public ISocialPlatformProvider Get(ContentPlatform platform)
    {
        if (!_providers.TryGetValue(platform, out var provider))
        {
            throw new UnsupportedSocialPlatformException(
                $"The platform {platform} is not a supported social platform.");
        }

        return provider;
    }
}

/// <summary>
/// Parses platform values coming from routes or query strings. Anything the
/// product does not support is rejected explicitly instead of silently
/// defaulting to a provider.
/// </summary>
public static class SocialPlatformParser
{
    public static bool TryParse(string? value, out ContentPlatform platform)
    {
        platform = default;

        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        switch (value.Trim().ToLowerInvariant())
        {
            case "linkedin":
                platform = ContentPlatform.LinkedIn;
                return true;
            case "x":
                platform = ContentPlatform.X;
                return true;
            default:
                return false;
        }
    }
}
