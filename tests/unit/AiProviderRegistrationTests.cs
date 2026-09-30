using Api.Application.AI;
using Api.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace Api.Tests;

public sealed class AiProviderRegistrationTests
{
    [Theory]
    [InlineData("OpenAI", typeof(OpenAiProvider))]
    [InlineData("Groq", typeof(OpenAiProvider))]
    [InlineData("OpenRouter", typeof(OpenAiProvider))]
    [InlineData("Gemini", typeof(GeminiProvider))]
    public void Configured_provider_is_resolved_from_dependency_injection(
        string providerName,
        Type expectedProviderType)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IHostEnvironment>(new TestHostEnvironment());
        services.AddConfiguredAiProvider(CreateConfiguration(providerName));
        using var serviceProvider = services.BuildServiceProvider();

        var provider = serviceProvider.GetRequiredService<IAiProvider>();
        var options = serviceProvider.GetRequiredService<
            Microsoft.Extensions.Options.IOptions<AiOptions>>().Value;

        Assert.IsType(expectedProviderType, provider);
        Assert.Equal(providerName, options.Provider);
    }

    [Fact]
    public void Unknown_provider_fails_during_registration()
    {
        var services = new ServiceCollection();

        var exception = Assert.Throws<InvalidOperationException>(() =>
            services.AddConfiguredAiProvider(CreateConfiguration("SomethingUnknown")));

        Assert.Contains("Unavailable, OpenAI, Groq, OpenRouter, or Gemini", exception.Message);
    }

    [Fact]
    public void Gemini_configuration_validates_without_openai_specific_settings()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddConfiguredAiProvider(CreateConfiguration("Gemini"));
        using var serviceProvider = services.BuildServiceProvider();

        var options = serviceProvider.GetRequiredService<
            Microsoft.Extensions.Options.IOptions<AiOptions>>().Value;

        Assert.Equal("Gemini", options.Provider);
        Assert.Equal("gemini-test-model", options.Model);
    }

    [Fact]
    public void OpenAI_retains_its_existing_default_model()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AI:Provider"] = "OpenAI",
                ["AI:ApiKey"] = "unit-test-placeholder",
                ["AI:TimeoutSeconds"] = "5"
            })
            .Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddConfiguredAiProvider(configuration);
        using var serviceProvider = services.BuildServiceProvider();

        var options = serviceProvider.GetRequiredService<
            Microsoft.Extensions.Options.IOptions<AiOptions>>().Value;

        Assert.Equal("gpt-4o-mini", options.Model);
    }

    [Theory]
    [InlineData("OpenAI", "https://api.openai.com/v1/")]
    [InlineData("Groq", "https://api.groq.com/openai/v1/")]
    [InlineData("OpenRouter", "https://openrouter.ai/api/v1/")]
    public void Compatible_provider_uses_its_configured_base_url(
        string providerName,
        string expectedBaseUrl)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddConfiguredAiProvider(CreateConfiguration(providerName));
        using var serviceProvider = services.BuildServiceProvider();

        var client = serviceProvider.GetRequiredService<IHttpClientFactory>()
            .CreateClient(nameof(IAiProvider));

        Assert.Equal(new Uri(expectedBaseUrl), client.BaseAddress);
    }

    [Fact]
    public void Compatible_provider_accepts_a_custom_https_base_url()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AI:Provider"] = "OpenRouter",
                ["AI:Model"] = "provider/model-id",
                ["AI:BaseUrl"] = "https://gateway.example/v1",
                ["AI:ApiKey"] = "unit-test-placeholder",
                ["AI:TimeoutSeconds"] = "5"
            })
            .Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddConfiguredAiProvider(configuration);
        using var serviceProvider = services.BuildServiceProvider();

        var client = serviceProvider.GetRequiredService<IHttpClientFactory>()
            .CreateClient(nameof(IAiProvider));

        Assert.Equal(new Uri("https://gateway.example/v1/"), client.BaseAddress);
    }

    [Fact]
    public void Compatible_provider_rejects_non_https_base_url()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AI:Provider"] = "Groq",
                ["AI:Model"] = "openai/gpt-oss-20b",
                ["AI:BaseUrl"] = "http://gateway.example/v1/",
                ["AI:ApiKey"] = "unit-test-placeholder",
                ["AI:TimeoutSeconds"] = "5"
            })
            .Build();
        var services = new ServiceCollection();
        services.AddConfiguredAiProvider(configuration);
        using var serviceProvider = services.BuildServiceProvider();

        Assert.Throws<Microsoft.Extensions.Options.OptionsValidationException>(() =>
            serviceProvider.GetRequiredService<
                Microsoft.Extensions.Options.IOptions<AiOptions>>().Value);
    }

    [Fact]
    public void Groq_is_resolved_from_double_underscore_environment_variables()
    {
        const string prefix = "MAYORX_TEST_";
        var values = new Dictionary<string, string>
        {
            [$"{prefix}AI__Provider"] = "Groq",
            [$"{prefix}AI__Model"] = "openai/gpt-oss-20b",
            [$"{prefix}AI__BaseUrl"] = "https://api.groq.com/openai/v1/",
            [$"{prefix}AI__ApiKey"] = "unit-test-placeholder",
            [$"{prefix}AI__TimeoutSeconds"] = "5"
        };
        var previousValues = values.Keys.ToDictionary(
            key => key,
            Environment.GetEnvironmentVariable);

        try
        {
            foreach (var (key, value) in values)
            {
                Environment.SetEnvironmentVariable(key, value);
            }

            var configuration = new ConfigurationBuilder()
                .AddEnvironmentVariables(prefix)
                .Build();
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSingleton<IHostEnvironment>(new TestHostEnvironment());
            services.AddConfiguredAiProvider(configuration);
            using var serviceProvider = services.BuildServiceProvider();

            var provider = serviceProvider.GetRequiredService<IAiProvider>();
            var options = serviceProvider.GetRequiredService<
                Microsoft.Extensions.Options.IOptions<AiOptions>>().Value;

            Assert.IsType<OpenAiProvider>(provider);
            Assert.Equal("Groq", options.Provider);
            Assert.Equal("openai/gpt-oss-20b", options.Model);
            Assert.Equal("https://api.groq.com/openai/v1/", options.BaseUrl);
        }
        finally
        {
            foreach (var (key, value) in previousValues)
            {
                Environment.SetEnvironmentVariable(key, value);
            }
        }
    }

    [Fact]
    public void Gemini_requires_an_explicit_model()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AI:Provider"] = "Gemini",
                ["AI:ApiKey"] = "unit-test-placeholder",
                ["AI:TimeoutSeconds"] = "5"
            })
            .Build();
        var services = new ServiceCollection();
        services.AddConfiguredAiProvider(configuration);
        using var serviceProvider = services.BuildServiceProvider();

        Assert.Throws<Microsoft.Extensions.Options.OptionsValidationException>(() =>
            serviceProvider.GetRequiredService<
                Microsoft.Extensions.Options.IOptions<AiOptions>>().Value);
    }

    private static IConfiguration CreateConfiguration(string provider) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AI:Provider"] = provider,
                ["AI:Model"] = provider.Equals("Gemini", StringComparison.OrdinalIgnoreCase)
                    ? "gemini-test-model"
                    : "openai/gpt-oss-20b",
                ["AI:BaseUrl"] = provider.ToUpperInvariant() switch
                {
                    "OPENAI" => "https://api.openai.com/v1/",
                    "GROQ" => "https://api.groq.com/openai/v1/",
                    "OPENROUTER" => "https://openrouter.ai/api/v1/",
                    _ => string.Empty
                },
                ["AI:ApiKey"] = "unit-test-placeholder",
                ["AI:TimeoutSeconds"] = "5"
            })
            .Build();

    private sealed class TestHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Development";
        public string ApplicationName { get; set; } = "Api.Tests";
        public string ContentRootPath { get; set; } = Directory.GetCurrentDirectory();
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}