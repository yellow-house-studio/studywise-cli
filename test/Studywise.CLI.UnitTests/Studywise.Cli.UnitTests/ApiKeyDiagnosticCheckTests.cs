using Studywise.Cli.Configuration;
using Studywise.Cli.Diagnostics;
using Studywise.Cli.Diagnostics.Checks;

namespace Studywise.CLI.UnitTests;

public class ApiKeyDiagnosticCheckTests
{
    [Fact]
    public async Task RunAsync_ReturnsPassWhenConfigContainsApiKey()
    {
        await RunWithTemporaryConfigPathAsync(async configPath =>
        {
            await File.WriteAllTextAsync(configPath, "{\"apiKey\":\"test-key\"}");
            var check = new ApiKeyDiagnosticCheck(configPath);

            var result = await check.RunAsync();

            Assert.Equal("api-key", result.Name);
            Assert.Equal(DiagnosticStatus.Pass, result.Status);
            Assert.Equal("API-nyckel: OK — finns (maskerad)", result.Message);
        });
    }

    [Theory]
    [InlineData("{\"apiKey\":\"\"}")]
    [InlineData("{\"apiKey\":\" \"}")]
    [InlineData("{\"apiKey\":\"   \"}")]
    public async Task RunAsync_ReturnsFailWhenApiKeyInConfigIsEmptyOrWhitespace(string configContent)
    {
        await RunWithTemporaryConfigPathAsync(async configPath =>
        {
            await File.WriteAllTextAsync(configPath, configContent);
            var check = new ApiKeyDiagnosticCheck(configPath);

            var result = await check.RunAsync();

            Assert.Equal(DiagnosticStatus.Fail, result.Status);
            Assert.Equal("API-nyckel: FAIL — saknas eller ar tom i config", result.Message);
        });
    }

    [Fact]
    public async Task RunAsync_ReturnsFailWhenApiKeyIsMissing()
    {
        await RunWithTemporaryConfigPathAsync(async configPath =>
        {
            await File.WriteAllTextAsync(configPath, "{}");
            var check = new ApiKeyDiagnosticCheck(configPath);

            var result = await check.RunAsync();

            Assert.Equal(DiagnosticStatus.Fail, result.Status);
            Assert.Equal("API-nyckel: FAIL — saknas eller ar tom i config", result.Message);
        });
    }

    [Fact]
    public async Task RunAsync_NeverLeaksApiKeyValueInMessage()
    {
        await RunWithTemporaryConfigPathAsync(async configPath =>
        {
            const string secretValue = "super-secret-key";
            await File.WriteAllTextAsync(configPath, $"{{\"apiKey\":\"{secretValue}\"}}");
            var check = new ApiKeyDiagnosticCheck(configPath);

            var result = await check.RunAsync();

            Assert.DoesNotContain(secretValue, result.Message, StringComparison.Ordinal);
            Assert.Contains("maskerad", result.Message, StringComparison.Ordinal);
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
