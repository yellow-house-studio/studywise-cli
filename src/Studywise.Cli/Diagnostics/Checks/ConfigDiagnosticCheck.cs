using Studywise.Cli.Configuration;

namespace Studywise.Cli.Diagnostics.Checks;

public sealed class ConfigDiagnosticCheck : IDiagnosticCheck
{
    public string Name => "config";

    public Task<DiagnosticCheckResult> RunAsync(CancellationToken cancellationToken = default)
    {
        var configPath = ApplicationConfig.GetConfigPath();

        if (File.Exists(configPath))
        {
            return Task.FromResult(new DiagnosticCheckResult(Name, DiagnosticStatus.Pass, $"Config: OK — {configPath}"));
        }

        return Task.FromResult(new DiagnosticCheckResult(Name, DiagnosticStatus.Warn, $"Config: WARN — missing ({configPath})"));
    }
}
