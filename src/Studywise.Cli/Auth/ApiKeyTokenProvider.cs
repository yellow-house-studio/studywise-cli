namespace Studywise.Cli.Auth;

public sealed class ApiKeyTokenProvider(string apiKey) : ITokenProvider
{
    public string GetToken()
    {
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new InvalidOperationException(
                "API key missing. Set STUDYWISE_API_KEY or add an apiKey entry to ~/.config/studywise/config.json.");
        }

        return apiKey;
    }
}
