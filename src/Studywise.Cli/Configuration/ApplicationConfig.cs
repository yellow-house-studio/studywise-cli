using System.Text.Json;

namespace Studywise.Cli.Configuration;

/// <summary>
/// Resolved CLI configuration: the API base URL, the API key, and the user agent.
/// Immutable. Build via <see cref="FromEnvironment"/>.
/// </summary>
public sealed class ApplicationConfig
{
    /// <summary>The Studywise API base URL (no trailing path).</summary>
    public string ApiBaseUrl { get; init; } = StudywiseDefaults.ApiBaseUrl;

    /// <summary>The configured API key (env wins, config file fallback). Empty when none.</summary>
    public string ApiKey { get; init; } = Environment.GetEnvironmentVariable("STUDYWISE_API_KEY") ?? string.Empty;

    /// <summary>The User-Agent string sent on outbound requests.</summary>
    public string UserAgent { get; init; } = StudywiseDefaults.UserAgent;

    /// <summary>
    /// Builds an <see cref="ApplicationConfig"/> from environment variables and (optionally)
    /// a config file. Resolution order:
    /// <list type="number">
    ///   <item><description><c>STUDYWISE_API_BASE_URL</c> (falls back to <see cref="StudywiseDefaults.ApiBaseUrl"/>).</description></item>
    ///   <item><description><c>STUDYWISE_API_KEY</c> env var, falling back to the <c>apiKey</c> / <c>api_key</c> field of the resolved config file.</description></item>
    /// </list>
    /// </summary>
    /// <param name="configPathOverride">Optional explicit path to the config JSON. When null, the path is resolved from env vars and the user-profile default.</param>
    /// <returns>The resolved configuration.</returns>
    public static ApplicationConfig FromEnvironment(string? configPathOverride = null)
    {
        var apiBaseUrl = Environment.GetEnvironmentVariable("STUDYWISE_API_BASE_URL");
        var apiKeyFromConfig = ReadApiKeyFromConfigFile(configPathOverride);
        var apiKeyFromEnvironment = Environment.GetEnvironmentVariable("STUDYWISE_API_KEY") ?? string.Empty;

        return new ApplicationConfig
        {
            ApiBaseUrl = apiBaseUrl ?? StudywiseDefaults.ApiBaseUrl,
            ApiKey = string.IsNullOrWhiteSpace(apiKeyFromConfig) ? apiKeyFromEnvironment : apiKeyFromConfig
        };
    }

    /// <summary>
    /// Reads the <c>apiKey</c> (or <c>api_key</c>) field from the config JSON at the resolved path.
    /// Returns an empty string on any I/O / parse / permission error so the diagnostic layer
    /// can treat "no key" and "couldn't read the config" the same way.
    /// </summary>
    /// <param name="configPathOverride">Optional explicit config path; otherwise resolved via <see cref="GetConfigPath"/>.</param>
    /// <returns>The configured API key, or empty string when none is set or readable.</returns>
    public static string ReadApiKeyFromConfigFile(string? configPathOverride = null)
    {
        var configPath = GetConfigPath(configPathOverride);

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
        catch (UnauthorizedAccessException)
        {
            return string.Empty;
        }
        catch (IOException)
        {
            return string.Empty;
        }
        catch (OperationCanceledException)
        {
            return string.Empty;
        }
    }

    /// <summary>
    /// Resolves the config file path. Order: explicit override, <c>STUDYWISE_CONFIG</c>
    /// (legacy), <c>STUDYWISE_CONFIG_PATH</c>, then <c>~/.config/studywise/config.json</c>.
    /// </summary>
    /// <param name="configPathOverride">Optional explicit override.</param>
    /// <returns>The absolute config path.</returns>
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

/// <summary>
/// Constants used across the CLI for naming, default endpoints, and shared header values.
/// </summary>
public static class StudywiseDefaults
{
    /// <summary>Named HttpClient key used by both raw HttpClient and the transport seam.</summary>
    public const string ApiName = "Studywise";

    /// <summary>Default Studywise API base URL.</summary>
    public const string ApiBaseUrl = "https://api.studywise.io";

    /// <summary>User-Agent header sent on every outbound request.</summary>
    public const string UserAgent = "Studywise-CLI/1.0";

    /// <summary>Request header carrying the API key on every outbound call.</summary>
    public const string ApiKeyHeaderName = "X-Studywise-Api-Key";

    /// <summary>Path of the API endpoint that validates the configured API key.</summary>
    public const string AuthVerifyPath = "/api/v1/auth/verify";
}
