namespace Studywise.Cli.Diagnostics;

public interface IDiagnosticCheck
{
    string Name { get; }

    Task<DiagnosticCheckResult> RunAsync(CancellationToken cancellationToken = default);
}
