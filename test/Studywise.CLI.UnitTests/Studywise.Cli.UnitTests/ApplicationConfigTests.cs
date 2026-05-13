using Studywise.Cli.Configuration;

namespace Studywise.CLI.UnitTests;

public class ApplicationConfigTests
{
    [Fact]
    public async Task FromEnvironment_UsesApiKeyFromConfigWhenApiKeyIsPresent()
    {
        var previousApiKey = Environment.GetEnvironmentVariable("STUDYWISE_API_KEY");
        var configPath = GetConfigPath();

        try
        {
            EnsureConfigDirectoryExists(configPath);
            await File.WriteAllTextAsync(configPath, "{\"apiKey\":\"config-key\",\"api_key\":\"snake-key\"}");
            Environment.SetEnvironmentVariable("STUDYWISE_API_KEY", "env-key");

            var config = ApplicationConfig.FromEnvironment(configPath);

            Assert.Equal("config-key", config.ApiKey);
        }
        finally
        {
            if (File.Exists(configPath))
            {
                File.Delete(configPath);
            }

            DeleteConfigDirectory(configPath);
            Environment.SetEnvironmentVariable("STUDYWISE_API_KEY", previousApiKey);
        }
    }

    [Fact]
    public async Task FromEnvironment_UsesSnakeCaseApiKeyWhenCamelCaseIsMissing()
    {
        var previousApiKey = Environment.GetEnvironmentVariable("STUDYWISE_API_KEY");
        var configPath = GetConfigPath();

        try
        {
            EnsureConfigDirectoryExists(configPath);
            await File.WriteAllTextAsync(configPath, "{\"api_key\":\"snake-key\"}");
            Environment.SetEnvironmentVariable("STUDYWISE_API_KEY", "env-key");

            var config = ApplicationConfig.FromEnvironment(configPath);

            Assert.Equal("snake-key", config.ApiKey);
        }
        finally
        {
            if (File.Exists(configPath))
            {
                File.Delete(configPath);
            }

            DeleteConfigDirectory(configPath);
            Environment.SetEnvironmentVariable("STUDYWISE_API_KEY", previousApiKey);
        }
    }

    [Fact]
    public async Task FromEnvironment_FallsBackToEnvironmentWhenConfigHasNoApiKey()
    {
        var previousApiKey = Environment.GetEnvironmentVariable("STUDYWISE_API_KEY");
        var configPath = GetConfigPath();

        try
        {
            EnsureConfigDirectoryExists(configPath);
            await File.WriteAllTextAsync(configPath, "{}");
            Environment.SetEnvironmentVariable("STUDYWISE_API_KEY", "env-key");

            var config = ApplicationConfig.FromEnvironment(configPath);

            Assert.Equal("env-key", config.ApiKey);
        }
        finally
        {
            if (File.Exists(configPath))
            {
                File.Delete(configPath);
            }

            DeleteConfigDirectory(configPath);
            Environment.SetEnvironmentVariable("STUDYWISE_API_KEY", previousApiKey);
        }
    }

    private static string GetConfigPath()
    {
        return Path.Combine(
            Path.GetTempPath(),
            $"studywise-tests-{Guid.NewGuid():N}",
            "config.json");
    }

    private static void EnsureConfigDirectoryExists(string configPath)
    {
        var configDirectory = Path.GetDirectoryName(configPath)!;

        Directory.CreateDirectory(configDirectory);
    }

    private static void DeleteConfigDirectory(string configPath)
    {
        var configDirectory = Path.GetDirectoryName(configPath);
        if (string.IsNullOrWhiteSpace(configDirectory) || !Directory.Exists(configDirectory))
        {
            return;
        }

        Directory.Delete(configDirectory, recursive: true);
    }
}
