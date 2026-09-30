using Studywise.Cli.Configuration;

namespace Studywise.Cli.Diagnostics.Checks;

public sealed class ApiKeyDiagnosticCheck(string? configPath = null) : IDiagnosticCheck
{
    public string Name => "api-key";

    public Task<DiagnosticCheckResult> RunAsync(CancellationToken cancellationToken = default)
    {
        var apiKey = ApplicationConfig.ReadApiKeyFromConfigFile(configPath);

        if (!string.IsNullOrWhiteSpace(apiKey))
        {
            return Task.FromResult(new DiagnosticCheckResult(Name, DiagnosticStatus.Pass, "API key: OK — present (masked)"));
        }

        return Task.FromResult(new DiagnosticCheckResult(Name, DiagnosticStatus.Fail, "API key: FAIL — missing or empty in config"));
    }
}
