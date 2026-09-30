using Api.Configuration;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Api.Tests;

public sealed class LocalDotEnvLoaderTests
{
    [Fact]
    public void Development_dotenv_overrides_process_values()
    {
        const string providerVariable = "AI__Provider";
        const string modelVariable = "AI__Model";
        const string baseUrlVariable = "AI__BaseUrl";
        const string keyVariable = "AI__ApiKey";
        const string timeoutVariable = "AI__TimeoutSeconds";
        var previousValues = new Dictionary<string, string?>
        {
            [providerVariable] = Environment.GetEnvironmentVariable(providerVariable),
            [modelVariable] = Environment.GetEnvironmentVariable(modelVariable),
            [baseUrlVariable] = Environment.GetEnvironmentVariable(baseUrlVariable),
            [keyVariable] = Environment.GetEnvironmentVariable(keyVariable),
            [timeoutVariable] = Environment.GetEnvironmentVariable(timeoutVariable)
        };
        var repositoryRoot = Path.Combine(
            Path.GetTempPath(),
            $"mayorx-dotenv-{Guid.NewGuid():N}");
        var startDirectory = Path.Combine(repositoryRoot, "apps", "api");

        try
        {
            foreach (var variable in previousValues.Keys)
            {
                Environment.SetEnvironmentVariable(variable, null);
            }

            Directory.CreateDirectory(Path.Combine(repositoryRoot, ".git"));
            Directory.CreateDirectory(startDirectory);
            File.WriteAllText(
                Path.Combine(repositoryRoot, ".env"),
                "AI__Provider=Groq\n" +
                "AI__Model=openai/gpt-oss-20b\n" +
                "AI__BaseUrl=https://api.groq.com/openai/v1/\n" +
                "AI__ApiKey=local-test-placeholder\n" +
                "AI__TimeoutSeconds=45\n");
            Environment.SetEnvironmentVariable(providerVariable, "Unavailable");
            Environment.SetEnvironmentVariable(modelVariable, "inherited-model");
            Environment.SetEnvironmentVariable(baseUrlVariable, "https://inherited.invalid/v1/");
            Environment.SetEnvironmentVariable(keyVariable, "inherited-test-placeholder");
            Environment.SetEnvironmentVariable(timeoutVariable, "10");

            LocalDotEnvLoader.LoadIfPresent("Development", startDirectory);

            Assert.Equal("Groq", Environment.GetEnvironmentVariable(providerVariable));
            Assert.Equal("openai/gpt-oss-20b", Environment.GetEnvironmentVariable(modelVariable));
            Assert.Equal("https://api.groq.com/openai/v1/", Environment.GetEnvironmentVariable(baseUrlVariable));
            Assert.Equal("local-test-placeholder", Environment.GetEnvironmentVariable(keyVariable));
            Assert.Equal("45", Environment.GetEnvironmentVariable(timeoutVariable));

            var configuration = new ConfigurationBuilder()
                .AddEnvironmentVariables()
                .Build();
            var options = configuration
                .GetSection(AiOptions.SectionName)
                .Get<AiOptions>();

            Assert.NotNull(options);
            Assert.Equal("Groq", options.Provider);
            Assert.Equal("openai/gpt-oss-20b", options.Model);
            Assert.Equal("https://api.groq.com/openai/v1/", options.BaseUrl);
            Assert.Equal("local-test-placeholder", options.ApiKey);
            Assert.Equal(45, options.TimeoutSeconds);
        }
        finally
        {
            foreach (var (variable, value) in previousValues)
            {
                Environment.SetEnvironmentVariable(variable, value);
            }

            if (Directory.Exists(repositoryRoot))
            {
                Directory.Delete(repositoryRoot, recursive: true);
            }
        }
    }

    [Fact]
    public void Non_development_environment_does_not_load_dotenv()
    {
        const string providerVariable = "AI__Provider";
        var previousValue = Environment.GetEnvironmentVariable(providerVariable);
        var repositoryRoot = Path.Combine(
            Path.GetTempPath(),
            $"mayorx-dotenv-{Guid.NewGuid():N}");

        try
        {
            Environment.SetEnvironmentVariable(providerVariable, null);
            Directory.CreateDirectory(Path.Combine(repositoryRoot, ".git"));
            File.WriteAllText(
                Path.Combine(repositoryRoot, ".env"),
                "AI__Provider=OpenAI\n");

            LocalDotEnvLoader.LoadIfPresent("Production", repositoryRoot);

            Assert.Null(Environment.GetEnvironmentVariable(providerVariable));
        }
        finally
        {
            Environment.SetEnvironmentVariable(providerVariable, previousValue);
            if (Directory.Exists(repositoryRoot))
            {
                Directory.Delete(repositoryRoot, recursive: true);
            }
        }
    }
}