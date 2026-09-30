using Studywise.Cli.Configuration;
using Studywise.Cli.Diagnostics;
using Studywise.Cli.Diagnostics.Checks;

namespace Studywise.CLI.UnitTests;

[Category("Unit")]
public class ApiKeyDiagnosticCheckTests
{
    [Test]
    public async Task RunAsync_ReturnsPassWhenConfigContainsApiKey()
    {
        await RunWithTemporaryConfigPathAsync(async configPath =>
        {
            await File.WriteAllTextAsync(configPath, "{\"apiKey\":\"test-key\"}");
            var check = new ApiKeyDiagnosticCheck(configPath);

            var result = await check.RunAsync();

            result.Name.Should().Be("api-key");
            result.Status.Should().Be(DiagnosticStatus.Pass);
            result.Message.Should().Be("API key: OK — present (masked)");
        });
    }

    [TestCase("{\"apiKey\":\"\"}")]
    [TestCase("{\"apiKey\":\" \"}")]
    [TestCase("{\"apiKey\":\"   \"}")]
    public async Task RunAsync_ReturnsFailWhenApiKeyInConfigIsEmptyOrWhitespace(string configContent)
    {
        await RunWithTemporaryConfigPathAsync(async configPath =>
        {
            await File.WriteAllTextAsync(configPath, configContent);
            var check = new ApiKeyDiagnosticCheck(configPath);

            var result = await check.RunAsync();

            result.Status.Should().Be(DiagnosticStatus.Fail);
            result.Message.Should().Be("API key: FAIL — missing or empty in config");
        });
    }

    [Test]
    public async Task RunAsync_ReturnsFailWhenApiKeyIsMissing()
    {
        await RunWithTemporaryConfigPathAsync(async configPath =>
        {
            await File.WriteAllTextAsync(configPath, "{}");
            var check = new ApiKeyDiagnosticCheck(configPath);

            var result = await check.RunAsync();

            result.Status.Should().Be(DiagnosticStatus.Fail);
            result.Message.Should().Be("API key: FAIL — missing or empty in config");
        });
    }

    [Test]
    public async Task RunAsync_NeverLeaksApiKeyValueInMessage()
    {
        await RunWithTemporaryConfigPathAsync(async configPath =>
        {
            const string secretValue = "super-secret-key";
            await File.WriteAllTextAsync(configPath, $"{{\"apiKey\":\"{secretValue}\"}}");
            var check = new ApiKeyDiagnosticCheck(configPath);

            var result = await check.RunAsync();

            result.Message.Should().NotContain(secretValue);
            result.Message.Should().Contain("masked");
        });
    }

    private static async Task RunWithTemporaryConfigPathAsync(Func<string, Task> testAction)
    {
        var temporaryDirectory = Path.Combine(Path.GetTempPath(), $"studywise-tests-{Guid.NewGuid():N}");
        var configPath = Path.Combine(temporaryDirectory, "config.json");

        Directory.CreateDirectory(temporaryDirectory);

        try
        {
            await testAction(configPath);
        }
        finally
        {
            if (Directory.Exists(temporaryDirectory))
            {
                Directory.Delete(temporaryDirectory, recursive: true);
            }
        }
    }
}
