using System.Security.Cryptography;
using Api.Application.Security;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace Api.Tests;

public sealed class SocialCredentialProtectionTests
{
    private const string AccessToken = "access-token-value-aaaaaaaaaaaaaaaa";
    private const string RefreshToken = "refresh-token-value-bbbbbbbbbbbbbb";

    [Fact]
    public void Access_token_survives_a_protect_and_unprotect_round_trip()
    {
        var protector = CreateProtector();

        var encrypted = protector.ProtectAccessToken(AccessToken);

        Assert.Equal(AccessToken, protector.UnprotectAccessToken(encrypted));
    }

    [Fact]
    public void Refresh_token_survives_a_protect_and_unprotect_round_trip()
    {
        var protector = CreateProtector();

        var encrypted = protector.ProtectRefreshToken(RefreshToken);

        Assert.Equal(RefreshToken, protector.UnprotectRefreshToken(encrypted));
    }

    [Fact]
    public void Protected_token_is_not_the_plaintext_token()
    {
        var protector = CreateProtector();

        var encrypted = protector.ProtectAccessToken(AccessToken);

        Assert.NotEqual(AccessToken, encrypted);
        Assert.DoesNotContain(AccessToken, encrypted, StringComparison.Ordinal);
    }

    [Fact]
    public void The_same_token_encrypts_to_a_different_ciphertext_each_time()
    {
        var protector = CreateProtector();

        var first = protector.ProtectAccessToken(AccessToken);
        var second = protector.ProtectAccessToken(AccessToken);

        Assert.NotEqual(first, second);
    }

    /// <summary>
    /// Access and refresh tokens are protected under different purposes. Without
    /// this, a value stored in one column could be replayed through the other.
    /// </summary>
    [Fact]
    public void An_access_token_cannot_be_unprotected_as_a_refresh_token()
    {
        var protector = CreateProtector();

        var encrypted = protector.ProtectAccessToken(AccessToken);

        Assert.Throws<CryptographicException>(
            () => protector.UnprotectRefreshToken(encrypted));
    }

    [Fact]
    public void A_refresh_token_cannot_be_unprotected_as_an_access_token()
    {
        var protector = CreateProtector();

        var encrypted = protector.ProtectRefreshToken(RefreshToken);

        Assert.Throws<CryptographicException>(
            () => protector.UnprotectAccessToken(encrypted));
    }

    [Fact]
    public void Purposes_are_distinct()
    {
        Assert.NotEqual(
            DataProtectionSocialCredentialProtector.AccessTokenPurpose,
            DataProtectionSocialCredentialProtector.RefreshTokenPurpose);
    }

    [Fact]
    public void A_tampered_ciphertext_is_rejected()
    {
        var protector = CreateProtector();
        var encrypted = protector.ProtectAccessToken(AccessToken);
        var tampered = encrypted[..^1] + (encrypted[^1] == 'A' ? 'B' : 'A');

        Assert.Throws<CryptographicException>(
            () => protector.UnprotectAccessToken(tampered));
    }

    [Fact]
    public void A_token_protected_by_one_provider_cannot_be_read_by_a_different_key_ring()
    {
        var encrypted = CreateProtector().ProtectAccessToken(AccessToken);

        var otherProtector = CreateProtector(keyRingDirectory: CreateTemporaryDirectory());

        Assert.Throws<CryptographicException>(
            () => otherProtector.UnprotectAccessToken(encrypted));
    }

    [Fact]
    public void Empty_tokens_are_rejected_rather_than_encrypted()
    {
        var protector = CreateProtector();

        Assert.Throws<ArgumentException>(() => protector.ProtectAccessToken(string.Empty));
        Assert.Throws<ArgumentException>(() => protector.ProtectRefreshToken("   "));
    }

    /// <summary>
    /// The local-development key ring must survive an application restart,
    /// otherwise every stored credential becomes unreadable on redeploy.
    /// </summary>
    [Fact]
    public void Encrypted_credentials_survive_a_restart_when_the_key_ring_is_persisted()
    {
        var keyRingDirectory = CreateTemporaryDirectory();
        var encrypted = CreateProtector(keyRingDirectory).ProtectAccessToken(AccessToken);

        var afterRestart = CreateProtector(keyRingDirectory);

        Assert.Equal(AccessToken, afterRestart.UnprotectAccessToken(encrypted));
    }

    [Fact]
    public void The_key_ring_is_written_to_the_configured_directory()
    {
        var keyRingDirectory = CreateTemporaryDirectory();

        CreateProtector(keyRingDirectory).ProtectAccessToken(AccessToken);

        var keyFiles = Directory.GetFiles(keyRingDirectory, "key-*.xml");
        Assert.NotEmpty(keyFiles);
    }

    [Fact]
    public void The_key_ring_directory_is_created_when_it_does_not_exist()
    {
        var parent = CreateTemporaryDirectory();
        var keyRingDirectory = Path.Combine(parent, "nested", "keys");

        CreateProtector(keyRingDirectory).ProtectAccessToken(AccessToken);

        Assert.True(Directory.Exists(keyRingDirectory));
    }

    [Fact]
    public void The_registration_persists_keys_and_resolves_the_protector()
    {
        var keyRingDirectory = Path.Combine(CreateTemporaryDirectory(), "keys");
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["DataProtection:KeyRingPath"] = keyRingDirectory,
                ["DataProtection:ApplicationName"] = "mayorx-content-engine-tests"
            })
            .Build();
        var services = new ServiceCollection();

        services.AddSocialCredentialProtection(configuration, new TestHostEnvironment());

        using var serviceProvider = services.BuildServiceProvider();
        var protector = serviceProvider
            .GetRequiredService<ISocialCredentialProtector>();

        var encrypted = protector.ProtectAccessToken(AccessToken);

        Assert.NotEmpty(Directory.GetFiles(keyRingDirectory, "key-*.xml"));
        Assert.Equal(AccessToken, protector.UnprotectAccessToken(encrypted));
    }

    /// <summary>
    /// A relative key ring path must resolve against the content root so the
    /// configured location is predictable.
    /// </summary>
    [Fact]
    public void A_relative_key_ring_path_resolves_against_the_content_root()
    {
        var contentRoot = CreateTemporaryDirectory();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["DataProtection:KeyRingPath"] = "keys",
                ["DataProtection:ApplicationName"] = "mayorx-content-engine-tests"
            })
            .Build();
        var services = new ServiceCollection();

        services.AddSocialCredentialProtection(
            configuration,
            new TestHostEnvironment { ContentRootPath = contentRoot });
        using var serviceProvider = services.BuildServiceProvider();
        var protector = serviceProvider.GetRequiredService<ISocialCredentialProtector>();

        protector.ProtectAccessToken(AccessToken);

        Assert.NotEmpty(Directory.GetFiles(Path.Combine(contentRoot, "keys"), "key-*.xml"));
    }

    private static ISocialCredentialProtector CreateProtector(
        string? keyRingDirectory = null)
    {
        var keyRing = keyRingDirectory ?? CreateTemporaryDirectory();
        var provider = DataProtectionProvider.Create(
            new DirectoryInfo(keyRing),
            builder => builder.SetApplicationName("mayorx-content-engine-tests"));

        return new DataProtectionSocialCredentialProtector(provider);
    }

    private static string CreateTemporaryDirectory()
    {
        var path = Path.Combine(
            Path.GetTempPath(),
            "mayorx-tests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);

        return path;
    }

    private sealed class TestHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Development";
        public string ApplicationName { get; set; } = "Api.Tests";
        public string ContentRootPath { get; set; } = Directory.GetCurrentDirectory();
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
