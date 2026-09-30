using Studywise.Cli.Configuration;

namespace Studywise.Cli.Diagnostics.Checks;

/// <summary>
/// Diagnostic check that reports whether a Studywise API key is configured in
/// the resolved config file or environment.
/// </summary>
public sealed class ApiKeyDiagnosticCheck : IDiagnosticCheck
{
    private readonly string? _configPath;

    /// <summary>
    /// Initializes the check against the resolved config path.
    /// </summary>
    /// <param name="configPath">Optional override for the config file path. Defaults to the path resolved by <see cref="ApplicationConfig.GetConfigPath"/>.</param>
    public ApiKeyDiagnosticCheck(string? configPath = null)
    {
        _configPath = configPath;
    }

    /// <inheritdoc />
    public string Name => "api-key";

    /// <summary>
    /// Runs the check. PASS when a non-whitespace key is configured; FAIL otherwise.
    /// The configured key value is never included in the result message.
    /// </summary>
    /// <param name="cancellationToken">Token observed for cancellation.</param>
    /// <returns>The check outcome.</returns>
    public Task<DiagnosticCheckResult> RunAsync(CancellationToken cancellationToken = default)
    {
        var apiKey = ApplicationConfig.ReadApiKeyFromConfigFile(_configPath);

        if (!string.IsNullOrWhiteSpace(apiKey))
        {
            return Task.FromResult(new DiagnosticCheckResult(Name, DiagnosticStatus.Pass, "API key: OK — present (masked)"));
        }

        return Task.FromResult(new DiagnosticCheckResult(Name, DiagnosticStatus.Fail, "API key: FAIL — missing or empty in config"));
    }
}
