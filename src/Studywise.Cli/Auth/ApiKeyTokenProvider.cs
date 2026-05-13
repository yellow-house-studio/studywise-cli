namespace Studywise.Cli.Auth;

public sealed class ApiKeyTokenProvider : ITokenProvider
{
    private readonly string _apiKey;

    public ApiKeyTokenProvider(string apiKey)
    {
        _apiKey = apiKey;
    }

    public string GetToken()
    {
        if (string.IsNullOrWhiteSpace(_apiKey))
        {
            throw new InvalidOperationException("API-nyckel saknas. Sätt STUDYWISE_API_KEY.");
        }

        return _apiKey;
    }
}
