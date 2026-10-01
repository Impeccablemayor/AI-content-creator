using System.Text.Json;
using Api.Configuration;
using Api.Domain.Enums;

namespace Api.Application.SocialAccounts;

public sealed class LinkedInSocialPlatformProvider : OAuthSocialPlatformProvider
{
    private readonly string _profileEndpoint;

    public LinkedInSocialPlatformProvider(
        HttpClient client,
        LinkedInOptions options)
        : base(
            client,
            options,
            options.AuthorizationEndpoint,
            options.TokenEndpoint)
    {
        _profileEndpoint = options.ProfileEndpoint;
    }

    public override ContentPlatform Platform => ContentPlatform.LinkedIn;

    public override async Task<SocialAccountProfile> GetProfileAsync(
        string accessToken,
        CancellationToken cancellationToken)
    {
        using var document = await GetProfileDocumentAsync(
            accessToken,
            _profileEndpoint,
            query: null,
            cancellationToken);
        var root = document.RootElement;

        var subject = ReadString(root, "sub");
        if (string.IsNullOrWhiteSpace(subject))
        {
            throw new SocialProviderRequestException(
                "LinkedIn did not return a subject identifier for the connected account.");
        }

        return new SocialAccountProfile(
            subject,
            ReadString(root, "email"),
            ReadString(root, "name"),
            ReadString(root, "picture"));
    }
}

public sealed class XSocialPlatformProvider : OAuthSocialPlatformProvider
{
    private const string ProfileQuery = "?user.fields=username,profile_image_url";

    private readonly string _profileEndpoint;

    public XSocialPlatformProvider(
        HttpClient client,
        XOptions options)
        : base(
            client,
            options,
            options.AuthorizationEndpoint,
            options.TokenEndpoint)
    {
        _profileEndpoint = options.ProfileEndpoint;
    }

    public override ContentPlatform Platform => ContentPlatform.X;

    public override async Task<SocialAccountProfile> GetProfileAsync(
        string accessToken,
        CancellationToken cancellationToken)
    {
        using var document = await GetProfileDocumentAsync(
            accessToken,
            _profileEndpoint,
            ProfileQuery,
            cancellationToken);

        if (!document.RootElement.TryGetProperty("data", out var data) ||
            data.ValueKind != JsonValueKind.Object)
        {
            throw new SocialProviderRequestException(
                "X did not return account data for the connected user.");
        }

        var id = ReadString(data, "id");
        if (string.IsNullOrWhiteSpace(id))
        {
            throw new SocialProviderRequestException(
                "X did not return an account identifier for the connected user.");
        }

        var username = ReadString(data, "username");

        return new SocialAccountProfile(
            id,
            username,
            ReadString(data, "name"),
            string.IsNullOrWhiteSpace(username) ? null : $"https://x.com/{username}");
    }
}
