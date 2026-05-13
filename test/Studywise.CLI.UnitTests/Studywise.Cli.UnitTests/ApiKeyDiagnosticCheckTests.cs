using Studywise.Cli.Auth;
using Studywise.Cli.Diagnostics;
using Studywise.Cli.Diagnostics.Checks;

namespace Studywise.CLI.UnitTests;

public class ApiKeyDiagnosticCheckTests
{
    [Fact]
    public async Task RunAsync_ReturnsPassWhenTokenProviderReturnsApiKey()
    {
        var check = new ApiKeyDiagnosticCheck(new ApiKeyTokenProvider("test-key"));

        var result = await check.RunAsync();

        Assert.Equal("api-key", result.Name);
        Assert.Equal(DiagnosticStatus.Pass, result.Status);
        Assert.Equal("API-nyckel: OK — finns (maskerad)", result.Message);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("   ")]
    public async Task RunAsync_ReturnsFailWhenApiKeyIsEmptyOrWhitespace(string apiKey)
    {
        var check = new ApiKeyDiagnosticCheck(new ApiKeyTokenProvider(apiKey));

        var result = await check.RunAsync();

        Assert.Equal(DiagnosticStatus.Fail, result.Status);
        Assert.Equal("API-nyckel: FAIL — saknas i environment variable", result.Message);
    }

    [Fact]
    public async Task RunAsync_ReturnsFailWhenApiKeyIsMissing()
    {
        var check = new ApiKeyDiagnosticCheck(new ApiKeyTokenProvider(string.Empty));

        var result = await check.RunAsync();

        Assert.Equal(DiagnosticStatus.Fail, result.Status);
        Assert.Equal("API-nyckel: FAIL — saknas i environment variable", result.Message);
    }

    [Fact]
    public async Task RunAsync_NeverLeaksApiKeyValueInMessage()
    {
        const string secretValue = "super-secret-key";
        var check = new ApiKeyDiagnosticCheck(new ApiKeyTokenProvider(secretValue));

        var result = await check.RunAsync();

        Assert.DoesNotContain(secretValue, result.Message, StringComparison.Ordinal);
        Assert.Contains("maskerad", result.Message, StringComparison.Ordinal);
    }
}
