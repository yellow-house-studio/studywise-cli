namespace Studywise.Cli.Diagnostics;

/// <summary>
/// Runs an ordered sequence of <see cref="IDiagnosticCheck"/>s and aggregates
/// the results into a <see cref="DiagnosticReport"/>.
/// </summary>
public interface IDiagnosticRunner
{
    /// <summary>
    /// Runs each check sequentially, awaiting each one in turn, and returns
    /// the aggregated report. Cancellation observed between checks.
    /// </summary>
    /// <param name="checks">The checks to run, in order.</param>
    /// <param name="cancellationToken">Token observed for cancellation.</param>
    /// <returns>The aggregated report.</returns>
    Task<DiagnosticReport> RunAsync(
        IEnumerable<IDiagnosticCheck> checks,
        CancellationToken cancellationToken = default);
}

/// <inheritdoc />
public sealed class DiagnosticRunner : IDiagnosticRunner
{
    /// <inheritdoc />
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
