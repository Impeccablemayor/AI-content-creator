using Api.Configuration;
using Api.Infrastructure.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Api.Application.AI;

public static class AiProviderRegistration
{
    public static IServiceCollection AddConfiguredAiProvider(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var provider = configuration[$"{AiOptions.SectionName}:Provider"]
            ?? "Unavailable";

        services.AddOptions<AiOptions>()
            .Bind(configuration.GetSection(AiOptions.SectionName))
            .Validate(
                options => options.Provider.Equals("Unavailable", StringComparison.OrdinalIgnoreCase) ||
                    (IsOpenAiCompatibleProvider(options.Provider) &&
                     !string.IsNullOrWhiteSpace(options.Model) &&
                     !string.IsNullOrWhiteSpace(options.ApiKey)) ||
                    (options.Provider.Equals("Gemini", StringComparison.OrdinalIgnoreCase) &&
                     !string.IsNullOrWhiteSpace(configuration[$"{AiOptions.SectionName}:Model"]) &&
                     !string.IsNullOrWhiteSpace(options.ApiKey)),
                "AI provider must be Unavailable, OpenAI, Groq, OpenRouter, or Gemini; configured providers require a model and API key.")
            .Validate(
                options => string.IsNullOrWhiteSpace(options.BaseUrl) ||
                    (Uri.TryCreate(options.BaseUrl, UriKind.Absolute, out var baseUri) &&
                     baseUri.Scheme == Uri.UriSchemeHttps),
                "AI base URL must be an absolute HTTPS URL.")
            .Validate(
                options => options.TimeoutSeconds is >= 1 and <= 120,
                "AI timeout must be between 1 and 120 seconds.")
            .ValidateOnStart();

        if (IsOpenAiCompatibleProvider(provider))
        {
            services.AddHttpClient<IAiProvider, OpenAiProvider>(ConfigureOpenAiCompatibleClient)
                .ConfigurePrimaryHttpMessageHandler(Ipv4PreferredHttpConnection.CreateHandler);
        }
        else if (provider.Equals("Gemini", StringComparison.OrdinalIgnoreCase))
        {
            services.AddHttpClient<IAiProvider, GeminiProvider>(ConfigureGeminiClient);
        }
        else if (provider.Equals("Unavailable", StringComparison.OrdinalIgnoreCase))
        {
            services.AddSingleton<IAiProvider, UnavailableAiProvider>();
        }
        else
        {
            throw new InvalidOperationException(
                "AI:Provider must be set to Unavailable, OpenAI, Groq, OpenRouter, or Gemini.");
        }

        return services;
    }

    private static bool IsOpenAiCompatibleProvider(string provider) =>
        provider.Equals("OpenAI", StringComparison.OrdinalIgnoreCase) ||
        provider.Equals("Groq", StringComparison.OrdinalIgnoreCase) ||
        provider.Equals("OpenRouter", StringComparison.OrdinalIgnoreCase);

    private static void ConfigureOpenAiCompatibleClient(
        IServiceProvider serviceProvider,
        HttpClient client)
    {
        var options = serviceProvider
            .GetRequiredService<Microsoft.Extensions.Options.IOptions<AiOptions>>()
            .Value;
        var baseUrl = string.IsNullOrWhiteSpace(options.BaseUrl)
            ? options.Provider.ToUpperInvariant() switch
            {
                "OPENAI" => "https://api.openai.com/v1/",
                "GROQ" => "https://api.groq.com/openai/v1/",
                "OPENROUTER" => "https://openrouter.ai/api/v1/",
                _ => throw new InvalidOperationException("No OpenAI-compatible base URL is configured.")
            }
            : options.BaseUrl.Trim();
        client.BaseAddress = new Uri(baseUrl.EndsWith('/') ? baseUrl : $"{baseUrl}/");
        client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
    }

    private static void ConfigureGeminiClient(
        IServiceProvider serviceProvider,
        HttpClient client)
    {
        var options = serviceProvider
            .GetRequiredService<Microsoft.Extensions.Options.IOptions<AiOptions>>()
            .Value;
        client.BaseAddress = new Uri("https://generativelanguage.googleapis.com/v1beta/");
        client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
    }
}