using Studywise.Cli.Diagnostics;

namespace Studywise.CLI.UnitTests;

[Category("Unit")]
public class DiagnosticRunnerTests
{
    [Test]
    public async Task RunAsync_ExecutesChecksInSequence()
    {
        var callOrder = new List<string>();
        var checks = new IDiagnosticCheck[]
        {
            new TrackingCheck("config", callOrder),
            new TrackingCheck("api-key", callOrder),
            new TrackingCheck("connection", callOrder)
        };

        var runner = new DiagnosticRunner();
        var report = await runner.RunAsync(checks);

        callOrder.Should().Equal("config", "api-key", "connection");
        report.Checks.Should().HaveCount(3);
    }

    private sealed class TrackingCheck(string name, List<string> callOrder) : IDiagnosticCheck
    {
        public string Name => name;

        public Task<DiagnosticCheckResult> RunAsync(CancellationToken cancellationToken = default)
        {
            callOrder.Add(name);
            return Task.FromResult(new DiagnosticCheckResult(name, DiagnosticStatus.Pass, $"{name}: OK"));
        }
    }
}
