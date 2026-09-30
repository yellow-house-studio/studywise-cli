using Studywise.Cli.Diagnostics;
using Studywise.Cli.Diagnostics.Formatting;

namespace Studywise.CLI.UnitTests;

[Category("Unit")]
public class TextDiagnosticReportFormatterTests
{
    [Test]
    public void Format_IncludesTitleMarkersAndSummary()
    {
        var report = new DiagnosticReport(
        [
            new DiagnosticCheckResult("config", DiagnosticStatus.Pass, "Config: OK"),
            new DiagnosticCheckResult("api-key", DiagnosticStatus.Fail, "API-nyckel: FAIL — saknas eller ar tom i config"),
            new DiagnosticCheckResult("connection", DiagnosticStatus.Warn, "Connection: WARN — /health svarade med 503")
        ]);

        var formatter = new TextDiagnosticReportFormatter();
        var text = formatter.Format(report);

        text.Should().Contain("Studywise CLI Diagnostics");
        text.Should().Contain("[PASS] Config: OK");
        text.Should().Contain("[FAIL] API-nyckel: FAIL — saknas eller ar tom i config");
        text.Should().Contain("[WARN] Connection: WARN — /health svarade med 503");
        text.Should().Contain("1 failed, 1 passed, 1 warning (exit code 1)");
    }

    [Test]
    public void Format_UsesPluralWarningsInSummary()
    {
        var report = new DiagnosticReport(
        [
            new DiagnosticCheckResult("connection", DiagnosticStatus.Warn, "Connection: WARN — timeout"),
            new DiagnosticCheckResult("api", DiagnosticStatus.Warn, "API: WARN — degraded")
        ]);

        var formatter = new TextDiagnosticReportFormatter();
        var text = formatter.Format(report);

        text.Should().Contain("0 failed, 0 passed, 2 warnings (exit code 0)");
    }
}
