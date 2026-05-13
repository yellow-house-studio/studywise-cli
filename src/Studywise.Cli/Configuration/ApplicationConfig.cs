namespace Studywise.Cli.Configuration;

public sealed class ApplicationConfig
{
    public string ApiBaseUrl { get; init; } = StudywiseDefaults.ApiBaseUrl;
    public string ApiKey { get; init; } = Environment.GetEnvironmentVariable("STUDYWISE_API_KEY") ?? string.Empty;
    public string UserAgent { get; init; } = StudywiseDefaults.UserAgent;
    
    public static ApplicationConfig FromEnvironment(string? configPathOverride = null)
    {
        _ = configPathOverride;
        var apiBaseUrl = Environment.GetEnvironmentVariable("STUDYWISE_API_BASE_URL");
        var apiKeyFromEnvironment = Environment.GetEnvironmentVariable("STUDYWISE_API_KEY") ?? string.Empty;
        
        return new ApplicationConfig
        {
            ApiBaseUrl = apiBaseUrl ?? StudywiseDefaults.ApiBaseUrl,
            ApiKey = apiKeyFromEnvironment
        };
    }

    public static string GetConfigPath(string? configPathOverride = null)
    {
        if (!string.IsNullOrWhiteSpace(configPathOverride))
        {
            return configPathOverride;
        }

        var legacyConfigPathFromEnvironment = Environment.GetEnvironmentVariable("STUDYWISE_CONFIG");
        if (!string.IsNullOrWhiteSpace(legacyConfigPathFromEnvironment))
        {
            return legacyConfigPathFromEnvironment;
        }

        var configPathFromEnvironment = Environment.GetEnvironmentVariable("STUDYWISE_CONFIG_PATH");
        if (!string.IsNullOrWhiteSpace(configPathFromEnvironment))
        {
            return configPathFromEnvironment;
        }

        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".config",
            "studywise",
            "config.json");
    }

}

public static class StudywiseDefaults
{
    public const string ApiName = "Studywise";
    public const string ApiBaseUrl = "https://api.studywise.io";
    public const string UserAgent = "Studywise-CLI/1.0";
}
