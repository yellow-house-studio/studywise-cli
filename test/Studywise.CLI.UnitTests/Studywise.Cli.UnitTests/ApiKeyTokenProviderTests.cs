using Studywise.Cli.Auth;

namespace Studywise.CLI.UnitTests;

[Category("Unit")]
public class ApiKeyTokenProviderTests
{
    [Test]
    public void GetToken_ReturnsConfiguredKey()
    {
        var provider = new ApiKeyTokenProvider("test-key");

        provider.GetToken().Should().Be("test-key");
    }

    [TestCase("")]
    [TestCase("   ")]
    public void GetToken_WhenKeyIsBlank_ThrowsInvalidOperationException(string blank)
    {
        var provider = new ApiKeyTokenProvider(blank);

        var act = () => provider.GetToken();

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*API key missing*");
    }

    [Test]
    public void GetToken_ErrorMessage_DoesNotLeakKeyValue()
    {
        const string secretValue = "super-secret-key";
        var provider = new ApiKeyTokenProvider("");

        var act = () => provider.GetToken();

        act.Should().Throw<InvalidOperationException>()
            .Where(ex => !ex.Message.Contains(secretValue, StringComparison.Ordinal));
    }
}
