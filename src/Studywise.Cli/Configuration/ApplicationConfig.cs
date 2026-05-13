using System.Text.Json;

namespace Studywise.Cli.Configuration;

public sealed class ApplicationConfig
{
    public string ApiBaseUrl { get; init; } = StudywiseDefaults.ApiBaseUrl;
    public string ApiKey { get; init; } = Environment.GetEnvironmentVariable("STUDYWISE_API_KEY") ?? string.Empty;
    public string UserAgent { get; init; } = StudywiseDefaults.UserAgent;
    
    public static ApplicationConfig FromEnvironment()
    {
        var apiBaseUrl = Environment.GetEnvironmentVariable("STUDYWISE_API_BASE_URL");
        var apiKeyFromConfig = ReadApiKeyFromConfigFile();
        var apiKeyFromEnvironment = Environment.GetEnvironmentVariable("STUDYWISE_API_KEY") ?? string.Empty;
        
        return new ApplicationConfig
        {
            ApiBaseUrl = apiBaseUrl ?? StudywiseDefaults.ApiBaseUrl,
            ApiKey = string.IsNullOrWhiteSpace(apiKeyFromConfig) ? apiKeyFromEnvironment : apiKeyFromConfig
        };
    }

    private static string ReadApiKeyFromConfigFile()
    {
        var configPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".config",
            "studywise",
            "config.json");

        if (!File.Exists(configPath))
        {
            return string.Empty;
        }

        try
        {
            using var fileStream = File.OpenRead(configPath);
            using var document = JsonDocument.Parse(fileStream);

            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return string.Empty;
            }

            if (TryGetStringProperty(document.RootElement, "apiKey", out var apiKey))
            {
                return apiKey;
            }

            if (TryGetStringProperty(document.RootElement, "api_key", out var snakeCaseApiKey))
            {
                return snakeCaseApiKey;
            }

            return string.Empty;
        }
        catch (JsonException)
        {
            return string.Empty;
        }
    }

    private static bool TryGetStringProperty(JsonElement source, string propertyName, out string value)
    {
        if (source.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.String)
        {
            value = property.GetString() ?? string.Empty;
            return true;
        }

        value = string.Empty;
        return false;
    }
}

public static class StudywiseDefaults
{
    public const string ApiName = "Studywise";
    public const string ApiBaseUrl = "https://api.studywise.io";
    public const string UserAgent = "Studywise-CLI/1.0";
}
