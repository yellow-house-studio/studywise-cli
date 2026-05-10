using Studywise.Cli.Configuration;
using Studywise.Cli.Diagnostics;
using Studywise.Cli.Diagnostics.Checks;

namespace Studywise.CLI.UnitTests;

public class ConnectionDiagnosticCheckBoundaryTests
{
    [Fact]
    public async Task RunAsync_WithInvalidBaseUrl_ReturnsWarnWithoutHttpCall()
    {
        var previousBaseUrl = Environment.GetEnvironmentVariable("STUDYWISE_API_BASE_URL");
        Environment.SetEnvironmentVariable("STUDYWISE_API_BASE_URL", "not-a-valid-uri");

        try
        {
            var config = ApplicationConfig.FromEnvironment();
            var check = new ConnectionDiagnosticCheck(config);

            var result = await check.RunAsync();

            Assert.Equal("connection", result.Name);
            Assert.Equal(DiagnosticStatus.Warn, result.Status);
            Assert.Contains("invalid base URL", result.Message);
        }
        finally
        {
            Environment.SetEnvironmentVariable("STUDYWISE_API_BASE_URL", previousBaseUrl);
        }
    }

    [Fact]
    public async Task RunAsync_WithCancelledToken_ReturnsWarnResult()
    {
        var previousBaseUrl = Environment.GetEnvironmentVariable("STUDYWISE_API_BASE_URL");
        Environment.SetEnvironmentVariable("STUDYWISE_API_BASE_URL", "http://127.0.0.1:65535");

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        try
        {
            var config = ApplicationConfig.FromEnvironment();
            var check = new ConnectionDiagnosticCheck(config);

            var result = await check.RunAsync(cts.Token);

            Assert.Equal("connection", result.Name);
            Assert.Equal(DiagnosticStatus.Warn, result.Status);
            Assert.Contains("could not reach /health", result.Message);
            Assert.Contains("TaskCanceledException", result.Message);
        }
        finally
        {
            Environment.SetEnvironmentVariable("STUDYWISE_API_BASE_URL", previousBaseUrl);
        }
    }
}
