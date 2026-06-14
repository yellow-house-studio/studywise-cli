using Studywise.Cli.Auth;

namespace Studywise.Cli.UnitTests;

public class ApiKeyTokenProviderTests
{
    [Fact]
    public void GetToken_WithValidKey_ReturnsConfiguredKey()
    {
        var provider = new ApiKeyTokenProvider("sk_test_1234");

        var token = provider.GetToken();

        Assert.Equal("sk_test_1234", token);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void GetToken_WithMissingKey_ThrowsExpectedMessage(string apiKey)
    {
        var provider = new ApiKeyTokenProvider(apiKey);

        var exception = Assert.Throws<InvalidOperationException>(() => provider.GetToken());

        Assert.Equal("API-nyckel saknas. Sätt STUDYWISE_API_KEY.", exception.Message);
    }
}
