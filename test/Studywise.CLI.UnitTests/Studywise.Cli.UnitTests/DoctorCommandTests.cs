using System.CommandLine;
using Studywise.Cli.Commands;

namespace Studywise.CLI.UnitTests;

public class DoctorCommandTests
{
    [Fact]
    public async Task InvokeAsync_WithJsonFlag_ReturnsSuccessWhenNoChecksFail()
    {
        var previousApiKey = Environment.GetEnvironmentVariable("STUDYWISE_API_KEY");
        var previousBaseUrl = Environment.GetEnvironmentVariable("STUDYWISE_API_BASE_URL");

        Environment.SetEnvironmentVariable("STUDYWISE_API_KEY", "test-key");
        Environment.SetEnvironmentVariable("STUDYWISE_API_BASE_URL", "http://127.0.0.1:65535");

        try
        {
            var root = new RootCommand();
            root.AddCommand(DoctorCommand.Create());

            var exitCode = await root.InvokeAsync("doctor --json");

            Assert.Equal(0, exitCode);
        }
        finally
        {
            Environment.SetEnvironmentVariable("STUDYWISE_API_KEY", previousApiKey);
            Environment.SetEnvironmentVariable("STUDYWISE_API_BASE_URL", previousBaseUrl);
        }
    }

    [Fact]
    public async Task InvokeAsync_ReturnsFailureWhenApiKeyMissing()
    {
        var previousApiKey = Environment.GetEnvironmentVariable("STUDYWISE_API_KEY");
        var previousBaseUrl = Environment.GetEnvironmentVariable("STUDYWISE_API_BASE_URL");

        Environment.SetEnvironmentVariable("STUDYWISE_API_KEY", null);
        Environment.SetEnvironmentVariable("STUDYWISE_API_BASE_URL", "http://127.0.0.1:65535");

        try
        {
            var root = new RootCommand();
            root.AddCommand(DoctorCommand.Create());

            var exitCode = await root.InvokeAsync("doctor");

            Assert.Equal(1, exitCode);
        }
        finally
        {
            Environment.SetEnvironmentVariable("STUDYWISE_API_KEY", previousApiKey);
            Environment.SetEnvironmentVariable("STUDYWISE_API_BASE_URL", previousBaseUrl);
        }
    }
}
