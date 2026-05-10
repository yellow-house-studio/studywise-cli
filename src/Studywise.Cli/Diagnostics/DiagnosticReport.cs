namespace Studywise.Cli.Diagnostics;

public sealed class DiagnosticReport
{
    public DiagnosticReport(IReadOnlyList<DiagnosticCheckResult> checks)
    {
        Checks = checks;
        GeneratedAtUtc = DateTimeOffset.UtcNow;
        PassedCount = checks.Count(c => c.Status == DiagnosticStatus.Pass);
        FailedCount = checks.Count(c => c.Status == DiagnosticStatus.Fail);
        WarningCount = checks.Count(c => c.Status == DiagnosticStatus.Warn);
    }

    public DateTimeOffset GeneratedAtUtc { get; }

    public IReadOnlyList<DiagnosticCheckResult> Checks { get; }

    public int PassedCount { get; }

    public int FailedCount { get; }

    public int WarningCount { get; }

    public bool IsSuccess => FailedCount == 0;
}
