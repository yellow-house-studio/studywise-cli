using System.Text.Json;
using Studywise.Cli.Diagnostics;
using Studywise.Cli.Diagnostics.Formatting;

namespace Studywise.CLI.UnitTests;

public class JsonDiagnosticReportFormatterTests
{
    [Fact]
    public void Format_ReturnsExpectedJsonShape()
    {
        var report = new DiagnosticReport(
        [
            new DiagnosticCheckResult("config", DiagnosticStatus.Pass, "Config: OK"),
            new DiagnosticCheckResult("api-key", DiagnosticStatus.Fail, "API-key: FAIL")
        ]);

        var formatter = new JsonDiagnosticReportFormatter();
        var json = formatter.Format(report);

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.True(root.TryGetProperty("generatedAtUtc", out var generatedAtUtc));
        Assert.Equal(JsonValueKind.String, generatedAtUtc.ValueKind);

        Assert.True(root.TryGetProperty("checks", out var checks));
        Assert.Equal(2, checks.GetArrayLength());

        Assert.True(checks[0].TryGetProperty("message", out var firstMessage));
        Assert.Equal("Config: OK", firstMessage.GetString());
        Assert.Equal("config", checks[0].GetProperty("name").GetString());
        Assert.Equal("pass", checks[0].GetProperty("status").GetString());

        Assert.True(checks[1].TryGetProperty("message", out var secondMessage));
        Assert.Equal("API-key: FAIL", secondMessage.GetString());
        Assert.Equal("api-key", checks[1].GetProperty("name").GetString());
        Assert.Equal("fail", checks[1].GetProperty("status").GetString());

        Assert.True(root.TryGetProperty("failedCount", out var failedCount));
        Assert.Equal(1, failedCount.GetInt32());
    }
}
