using System.Text.Json;
using Studywise.Cli.Diagnostics;
using Studywise.Cli.Formatting;

namespace Studywise.CLI.UnitTests;

[Category("Unit")]
public class JsonReporterTests
{
    [Test]
    public void Format_ReturnsExpectedJsonShape()
    {
        var report = new DiagnosticReport(
        [
            new DiagnosticCheckResult("config", DiagnosticStatus.Pass, "Config: OK"),
            new DiagnosticCheckResult("api-key", DiagnosticStatus.Fail, "API-key: FAIL")
        ]);

        var json = JsonReporter.Format(report);

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        root.TryGetProperty("generatedAtUtc", out var generatedAtUtc).Should().BeTrue();
        generatedAtUtc.ValueKind.Should().Be(JsonValueKind.String);
        DateTimeOffset.TryParse(generatedAtUtc.GetString(), out _).Should().BeTrue();

        root.TryGetProperty("checks", out var checks).Should().BeTrue();
        checks.ValueKind.Should().Be(JsonValueKind.Array);
        checks.GetArrayLength().Should().Be(2);

        checks[0].TryGetProperty("message", out var firstMessage).Should().BeTrue();
        firstMessage.GetString().Should().Be("Config: OK");
        checks[0].GetProperty("name").GetString().Should().Be("config");
        checks[0].GetProperty("status").GetString().Should().Be("pass");

        checks[1].TryGetProperty("message", out var secondMessage).Should().BeTrue();
        secondMessage.GetString().Should().Be("API-key: FAIL");
        checks[1].GetProperty("name").GetString().Should().Be("api-key");
        checks[1].GetProperty("status").GetString().Should().Be("fail");

        root.TryGetProperty("failedCount", out var failedCount).Should().BeTrue();
        failedCount.GetInt32().Should().Be(1);
        root.TryGetProperty("passedCount", out var passedCount).Should().BeTrue();
        passedCount.GetInt32().Should().Be(1);
        root.TryGetProperty("warningCount", out var warningCount).Should().BeTrue();
        warningCount.GetInt32().Should().Be(0);
        root.TryGetProperty("isSuccess", out var isSuccess).Should().BeTrue();
        isSuccess.GetBoolean().Should().BeFalse();
    }
}
