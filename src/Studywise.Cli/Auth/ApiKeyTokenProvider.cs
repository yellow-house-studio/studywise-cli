namespace Studywise.Cli.Auth;

/// <summary>
/// <see cref="ITokenProvider"/> that returns an already-resolved API key string.
/// The key is supplied at construction time so the credential source (env, config file,
/// future OS keychain) is decided upstream in <see cref="Configuration.ApplicationConfig"/>.
/// </summary>
/// <param name="apiKey">The resolved API key. May be empty; <see cref="GetToken"/> will throw in that case.</param>
public sealed class ApiKeyTokenProvider(string apiKey) : ITokenProvider
{
    /// <summary>
    /// Returns the configured API key.
    /// </summary>
    /// <returns>The configured API key string.</returns>
    /// <exception cref="System.InvalidOperationException">
    /// Thrown when the configured key is null, empty, or whitespace.
    /// The message intentionally does not include the key value so a misconfigured
    /// credential cannot leak into logs or exception traces.
    /// </exception>
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
