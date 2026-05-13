using Studywise.Cli.Configuration;

namespace Studywise.CLI.UnitTests;

public class ApplicationConfigTests
{
    [Fact]
    public async Task FromEnvironment_UsesApiKeyFromConfigWhenApiKeyIsPresent()
    {
        var previousApiKey = Environment.GetEnvironmentVariable("STUDYWISE_API_KEY");
        var configPath = GetConfigPath();
        var originalConfig = File.Exists(configPath) ? await File.ReadAllTextAsync(configPath) : null;

        try
        {
            EnsureConfigDirectoryExists();
            await File.WriteAllTextAsync(configPath, "{\"apiKey\":\"config-key\",\"api_key\":\"snake-key\"}");
            Environment.SetEnvironmentVariable("STUDYWISE_API_KEY", "env-key");

            var config = ApplicationConfig.FromEnvironment();

            Assert.Equal("config-key", config.ApiKey);
        }
        finally
        {
            await RestoreConfigFileAsync(configPath, originalConfig);
            Environment.SetEnvironmentVariable("STUDYWISE_API_KEY", previousApiKey);
        }
    }

    [Fact]
    public async Task FromEnvironment_UsesSnakeCaseApiKeyWhenCamelCaseIsMissing()
    {
        var previousApiKey = Environment.GetEnvironmentVariable("STUDYWISE_API_KEY");
        var configPath = GetConfigPath();
        var originalConfig = File.Exists(configPath) ? await File.ReadAllTextAsync(configPath) : null;

        try
        {
            EnsureConfigDirectoryExists();
            await File.WriteAllTextAsync(configPath, "{\"api_key\":\"snake-key\"}");
            Environment.SetEnvironmentVariable("STUDYWISE_API_KEY", "env-key");

            var config = ApplicationConfig.FromEnvironment();

            Assert.Equal("snake-key", config.ApiKey);
        }
        finally
        {
            await RestoreConfigFileAsync(configPath, originalConfig);
            Environment.SetEnvironmentVariable("STUDYWISE_API_KEY", previousApiKey);
        }
    }

    [Fact]
    public async Task FromEnvironment_FallsBackToEnvironmentWhenConfigHasNoApiKey()
    {
        var previousApiKey = Environment.GetEnvironmentVariable("STUDYWISE_API_KEY");
        var configPath = GetConfigPath();
        var originalConfig = File.Exists(configPath) ? await File.ReadAllTextAsync(configPath) : null;

        try
        {
            EnsureConfigDirectoryExists();
            await File.WriteAllTextAsync(configPath, "{}");
            Environment.SetEnvironmentVariable("STUDYWISE_API_KEY", "env-key");

            var config = ApplicationConfig.FromEnvironment();

            Assert.Equal("env-key", config.ApiKey);
        }
        finally
        {
            await RestoreConfigFileAsync(configPath, originalConfig);
            Environment.SetEnvironmentVariable("STUDYWISE_API_KEY", previousApiKey);
        }
    }

    private static string GetConfigPath()
    {
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".config",
            "studywise",
            "config.json");
    }

    private static void EnsureConfigDirectoryExists()
    {
        var configDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".config",
            "studywise");

        Directory.CreateDirectory(configDirectory);
    }

    private static async Task RestoreConfigFileAsync(string configPath, string? originalConfig)
    {
        if (originalConfig is null)
        {
            if (File.Exists(configPath))
            {
                File.Delete(configPath);
            }

            return;
        }

        await File.WriteAllTextAsync(configPath, originalConfig);
    }
}
