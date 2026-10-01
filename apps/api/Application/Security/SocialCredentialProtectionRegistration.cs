using Api.Configuration;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Api.Application.Security;

public static class SocialCredentialProtectionRegistration
{
    public static IServiceCollection AddSocialCredentialProtection(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        services.AddOptions<DataProtectionKeyRingOptions>()
            .Bind(configuration.GetSection(DataProtectionKeyRingOptions.SectionName))
            .Validate(
                options => !string.IsNullOrWhiteSpace(options.ApplicationName),
                "Data protection application name is required.")
            .ValidateOnStart();

        var options = configuration
            .GetSection(DataProtectionKeyRingOptions.SectionName)
            .Get<DataProtectionKeyRingOptions>()
            ?? new DataProtectionKeyRingOptions();

        var dataProtection = services.AddDataProtection();
        dataProtection.SetApplicationName(options.ApplicationName);

        if (!string.IsNullOrWhiteSpace(options.KeyRingPath))
        {
            var keyRingDirectory = Path.IsPathRooted(options.KeyRingPath)
                ? options.KeyRingPath
                : Path.Combine(
                    environment.ContentRootPath,
                    options.KeyRingPath);

            Directory.CreateDirectory(keyRingDirectory);
            dataProtection.PersistKeysToFileSystem(new DirectoryInfo(keyRingDirectory));
        }

        services.AddSingleton<ISocialCredentialProtector,
            DataProtectionSocialCredentialProtector>();

        return services;
    }
}
