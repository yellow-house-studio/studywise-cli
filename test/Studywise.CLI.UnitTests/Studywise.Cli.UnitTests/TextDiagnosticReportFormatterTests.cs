using Studywise.Cli.Diagnostics;
using Studywise.Cli.Diagnostics.Formatting;

namespace Studywise.CLI.UnitTests;

public class TextDiagnosticReportFormatterTests
{
    [Fact]
    public void Format_IncludesTitleMarkersAndSummary()
    {
        var report = new DiagnosticReport(
        [
            new DiagnosticCheckResult("config", DiagnosticStatus.Pass, "Config: OK"),
            new DiagnosticCheckResult("api-key", DiagnosticStatus.Fail, "API-nyckel: FAIL — saknas i environment variable"),
            new DiagnosticCheckResult("connection", DiagnosticStatus.Warn, "Connection: WARN — /health svarade med 503")
        ]);

        var formatter = new TextDiagnosticReportFormatter();
        var text = formatter.Format(report);

        Assert.Contains("Studywise CLI Diagnostics", text);
        Assert.Contains("[PASS] Config: OK", text);
        Assert.Contains("[FAIL] API-nyckel: FAIL — saknas i environment variable", text);
        Assert.Contains("[WARN] Connection: WARN — /health svarade med 503", text);
        Assert.Contains("1 failed, 1 passed, 1 warning (exit code 1)", text);
    }
}
