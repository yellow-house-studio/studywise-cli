using Studywise.Cli.Diagnostics;

namespace Studywise.CLI.UnitTests;

public class DiagnosticRunnerTests
{
    [Fact]
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

        Assert.Equal(new[] { "config", "api-key", "connection" }, callOrder);
        Assert.Equal(3, report.Checks.Count);
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
