namespace Studywise.Cli.Auth;

/// <summary>
/// Source of the credential used by <see cref="ApiKeyDelegatingHandler"/> on outbound requests.
/// Implementations decide where the credential lives (env, file, OS keychain, future Vault).
/// </summary>
public interface ITokenProvider
{
    /// <summary>
    /// Returns the credential to attach to the next outbound request.
    /// </summary>
    /// <returns>The credential string.</returns>
    /// <exception cref="System.InvalidOperationException">
    /// Thrown when no credential is configured. The message should not contain the credential value.
    /// </exception>
    string GetToken();
}
