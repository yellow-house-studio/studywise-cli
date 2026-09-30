namespace Studywise.Cli.Diagnostics;

/// <summary>
/// Aggregated outcome of running multiple <see cref="IDiagnosticCheck"/>s,
/// with per-status counts and a derived success flag.
/// </summary>
public sealed class DiagnosticReport
{
    /// <summary>
    /// Builds a report from a set of check results, computing per-status counts
    /// and the report timestamp at construction time.
    /// </summary>
    /// <param name="checks">The check results in the order they were executed.</param>
    public DiagnosticReport(IReadOnlyList<DiagnosticCheckResult> checks)
    {
        Checks = checks;
        GeneratedAtUtc = DateTimeOffset.UtcNow;
        PassedCount = checks.Count(c => c.Status == DiagnosticStatus.Pass);
        FailedCount = checks.Count(c => c.Status == DiagnosticStatus.Fail);
        WarningCount = checks.Count(c => c.Status == DiagnosticStatus.Warn);
    }

    /// <summary>UTC timestamp at which the report was generated.</summary>
    public DateTimeOffset GeneratedAtUtc { get; }

    /// <summary>The individual check results, in execution order.</summary>
    public IReadOnlyList<DiagnosticCheckResult> Checks { get; }

    /// <summary>Number of checks with <see cref="DiagnosticStatus.Pass"/>.</summary>
    public int PassedCount { get; }

    /// <summary>Number of checks with <see cref="DiagnosticStatus.Fail"/>.</summary>
    public int FailedCount { get; }

    /// <summary>Number of checks with <see cref="DiagnosticStatus.Warn"/>.</summary>
    public int WarningCount { get; }

    /// <summary>True when no check failed (warnings are allowed).</summary>
    public bool IsSuccess => FailedCount == 0;
}
