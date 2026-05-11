namespace Studywise.Cli.Diagnostics;

public interface IDiagnosticRunner
{
    Task<DiagnosticReport> RunAsync(
        IEnumerable<IDiagnosticCheck> checks,
        CancellationToken cancellationToken = default);
}

public sealed class DiagnosticRunner : IDiagnosticRunner
{
    public async Task<DiagnosticReport> RunAsync(
        IEnumerable<IDiagnosticCheck> checks,
        CancellationToken cancellationToken = default)
    {
        var results = new List<DiagnosticCheckResult>();

        foreach (var check in checks)
        {
            results.Add(await check.RunAsync(cancellationToken));
        }

        return new DiagnosticReport(results);
    }
}