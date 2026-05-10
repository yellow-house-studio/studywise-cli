namespace Studywise.Cli.Diagnostics;

public sealed class DiagnosticRunner
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
