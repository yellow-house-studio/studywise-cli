namespace Studywise.Cli.Configuration;

public sealed class ApplicationConfig
{
    public string ApiBaseUrl { get; init; } = StudywiseDefaults.ApiBaseUrl;
    public string ApiKey { get; init; } = Environment.GetEnvironmentVariable("STUDYWISE_API_KEY") ?? string.Empty;
    public string UserAgent { get; init; } = StudywiseDefaults.UserAgent;
    
    public static ApplicationConfig FromEnvironment()
    {
        var apiBaseUrl = Environment.GetEnvironmentVariable("STUDYWISE_API_BASE_URL");
        
        return new ApplicationConfig
        {
            ApiBaseUrl = apiBaseUrl ?? StudywiseDefaults.ApiBaseUrl
        };
    }
}

public static class StudywiseDefaults
{
    public const string ApiName = "Studywise";
    public const string ApiBaseUrl = "https://api.studywise.io";
    public const string UserAgent = "Studywise-CLI/1.0";
}