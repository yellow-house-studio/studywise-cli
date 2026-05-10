using System.Net.Http;
using Studywise.Cli.Diagnostics;
using Studywise.Cli.Diagnostics.Checks;
using Xunit;

namespace Studywise.CLI.UnitTests;

public class ConnectionDiagnosticCheckBoundaryTests
{
    [Fact]
    public async Task RunAsync_WithCancelledToken_ReturnsWarnResult()
    {
        using var httpClient = new HttpClient { BaseAddress = new Uri("http://127.0.0.1:65535") };

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var check = new ConnectionDiagnosticCheck(httpClient);

        var result = await check.RunAsync(cts.Token);

        Assert.Equal("connection", result.Name);
        Assert.Equal(DiagnosticStatus.Warn, result.Status);
        Assert.Contains("could not reach /health", result.Message);
        Assert.Contains("TaskCanceledException", result.Message);
    }
}
