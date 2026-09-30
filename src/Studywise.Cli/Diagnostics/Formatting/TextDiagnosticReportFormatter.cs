using System.Text;

namespace Studywise.Cli.Diagnostics.Formatting;

/// <summary>
/// Renders a <see cref="DiagnosticReport"/> as a human-readable text block
/// (header, per-check lines with status markers, and a summary line).
/// </summary>
public sealed class TextDiagnosticReportFormatter
{
    /// <summary>
    /// Formats the report as text.
    /// </summary>
    /// <param name="report">The report to render.</param>
    /// <returns>The formatted text, including a trailing newline before the summary.</returns>
    public string Format(DiagnosticReport report)
    {
        var builder = new StringBuilder();
        builder.AppendLine("Studywise CLI Diagnostics");
        builder.AppendLine();

        foreach (var check in report.Checks)
        {
            builder.AppendLine($"[{ToMarker(check.Status)}] {check.Message}");
        }

        builder.AppendLine();
        builder.Append(Summary(report));
        return builder.ToString();
    }

    private static string ToMarker(DiagnosticStatus status) => status switch
    {
        DiagnosticStatus.Pass => "PASS",
        DiagnosticStatus.Fail => "FAIL",
        DiagnosticStatus.Warn => "WARN",
        _ => "UNKNOWN"
    };

    private static string Summary(DiagnosticReport report)
    {
        if (report.FailedCount == 0 && report.WarningCount == 0)
        {
            return $"All checks passed ({report.PassedCount}/{report.Checks.Count})";
        }

        var exitCode = report.IsSuccess ? 0 : 1;
        return $"{report.FailedCount} failed, {report.PassedCount} passed, {report.WarningCount} {Pluralize(report.WarningCount, "warning", "warnings")} (exit code {exitCode})";
    }

    private static string Pluralize(int count, string singular, string plural) => count == 1 ? singular : plural;
}
