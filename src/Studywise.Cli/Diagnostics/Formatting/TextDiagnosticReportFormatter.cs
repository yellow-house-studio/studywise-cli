using System.Text;

namespace Studywise.Cli.Diagnostics.Formatting;

public sealed class TextDiagnosticReportFormatter
{
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
