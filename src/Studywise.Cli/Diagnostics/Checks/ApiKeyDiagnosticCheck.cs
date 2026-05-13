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
            return Task.FromResult(new DiagnosticCheckResult(Name, DiagnosticStatus.Pass, "API-nyckel: OK — finns (maskerad)"));
        }

        return Task.FromResult(new DiagnosticCheckResult(Name, DiagnosticStatus.Fail, "API-nyckel: FAIL — saknas eller ar tom i config"));
    }
}
