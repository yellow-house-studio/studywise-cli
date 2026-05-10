namespace Studywise.Cli.Diagnostics.Checks;

public sealed class ConfigDiagnosticCheck : IDiagnosticCheck
{
    public string Name => "config";

    public Task<DiagnosticCheckResult> RunAsync(CancellationToken cancellationToken = default)
    {
        var configPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".config",
            "studywise",
            "config.json");

        if (File.Exists(configPath))
        {
            return Task.FromResult(new DiagnosticCheckResult(Name, DiagnosticStatus.Pass, $"Config: OK — {configPath}"));
        }

        return Task.FromResult(new DiagnosticCheckResult(Name, DiagnosticStatus.Warn, $"Config: WARN — missing ({configPath})"));
    }
}
