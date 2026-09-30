using System.Net;
using System.Net.Http;
using System.Text.Json;
using Moq;
using Studywise.Cli.Configuration;
using Studywise.Cli.Diagnostics;
using Studywise.Cli.Diagnostics.Checks;
using Studywise.Cli.Http;

namespace Studywise.CLI.UnitTests;

[Category("Unit")]
public class AuthVerifyDiagnosticCheckTests
{
    [Test]
    public async Task RunAsync_NoKeyConfigured_ReturnsWarnSkip()
    {
        var transport = new Mock<IStudywiseTransport>(MockBehavior.Strict);
        var check = new AuthVerifyDiagnosticCheck(transport.Object, new ApplicationConfig { ApiKey = "" });

        var result = await check.RunAsync();

        result.Name.Should().Be("auth-verify");
        result.Status.Should().Be(DiagnosticStatus.Warn);
        result.Message.Should().Contain("SKIP").And.Contain("no API key configured");
        transport.VerifyNoOtherCalls();
    }

    [Test]
    public async Task RunAsync_KeyWhitespaceOnly_ReturnsWarnSkip()
    {
        var transport = new Mock<IStudywiseTransport>(MockBehavior.Strict);
        var check = new AuthVerifyDiagnosticCheck(transport.Object, new ApplicationConfig { ApiKey = "   " });

        var result = await check.RunAsync();

        result.Status.Should().Be(DiagnosticStatus.Warn);
        result.Message.Should().Contain("SKIP");
        transport.VerifyNoOtherCalls();
    }

    [Test]
    public async Task RunAsync_Success200_ReturnsPassWithUserAndMethod()
    {
        var transport = new Mock<IStudywiseTransport>();
        transport.Setup(t => t.SendAsync(It.IsAny<HttpRequestMessage>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"userId\":\"users/abc-123\",\"authMethod\":\"ApiKey\"}")
            });
        var check = new AuthVerifyDiagnosticCheck(transport.Object, new ApplicationConfig { ApiKey = "test-key" });

        var result = await check.RunAsync();

        result.Status.Should().Be(DiagnosticStatus.Pass);
        result.Message.Should().Contain("users/abc-123");
        result.Message.Should().Contain("ApiKey");
    }

    [Test]
    public async Task RunAsync_Unauthorized_ReturnsFailInvalidOrRevoked()
    {
        var transport = new Mock<IStudywiseTransport>();
        transport.Setup(t => t.SendAsync(It.IsAny<HttpRequestMessage>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.Unauthorized)
            {
                Content = new StringContent("{\"error\":\"InvalidKey\"}")
            });
        var check = new AuthVerifyDiagnosticCheck(transport.Object, new ApplicationConfig { ApiKey = "bad-key" });

        var result = await check.RunAsync();

        result.Status.Should().Be(DiagnosticStatus.Fail);
        result.Message.Should().Contain("invalid or revoked");
    }

    [Test]
    public async Task RunAsync_UnauthorizedWithEmptyBody_ReturnsFailInvalidOrRevoked()
    {
        var transport = new Mock<IStudywiseTransport>();
        transport.Setup(t => t.SendAsync(It.IsAny<HttpRequestMessage>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.Unauthorized)
            {
                Content = new StringContent(string.Empty)
            });
        var check = new AuthVerifyDiagnosticCheck(transport.Object, new ApplicationConfig { ApiKey = "bad-key" });

        var result = await check.RunAsync();

        result.Status.Should().Be(DiagnosticStatus.Fail);
        result.Message.Should().Contain("invalid or revoked");
    }

    [TestCase(HttpStatusCode.InternalServerError, 500)]
    [TestCase(HttpStatusCode.ServiceUnavailable, 503)]
    [TestCase(HttpStatusCode.BadGateway, 502)]
    public async Task RunAsync_NonSuccessNonUnauthorizedStatus_ReturnsFailWithStatusCode(HttpStatusCode status, int expectedCode)
    {
        var transport = new Mock<IStudywiseTransport>();
        transport.Setup(t => t.SendAsync(It.IsAny<HttpRequestMessage>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HttpResponseMessage(status));
        var check = new AuthVerifyDiagnosticCheck(transport.Object, new ApplicationConfig { ApiKey = "test-key" });

        var result = await check.RunAsync();

        result.Status.Should().Be(DiagnosticStatus.Fail);
        result.Message.Should().Contain($"server returned {expectedCode}");
    }

    [Test]
    public async Task RunAsync_RequestTimeout_ReturnsFailWithTimeoutMessage()
    {
        var transport = new Mock<IStudywiseTransport>();
        transport.Setup(t => t.SendAsync(It.IsAny<HttpRequestMessage>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new TaskCanceledException("request timed out"));
        var check = new AuthVerifyDiagnosticCheck(transport.Object, new ApplicationConfig { ApiKey = "test-key" });

        var result = await check.RunAsync();

        result.Status.Should().Be(DiagnosticStatus.Fail);
        result.Message.Should().Contain("timeout after 5s");
        result.Message.Should().Contain("/api/v1/auth/verify");
    }

    [Test]
    public async Task RunAsync_UnreachableHost_ReturnsFailWithUnreachableMessage()
    {
        var transport = new Mock<IStudywiseTransport>();
        transport.Setup(t => t.SendAsync(It.IsAny<HttpRequestMessage>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Name or service not known"));
        var check = new AuthVerifyDiagnosticCheck(transport.Object, new ApplicationConfig { ApiKey = "test-key" });

        var result = await check.RunAsync();

        result.Status.Should().Be(DiagnosticStatus.Fail);
        result.Message.Should().Contain("could not reach");
        result.Message.Should().Contain("HttpRequestException");
    }

    [Test]
    public async Task RunAsync_CallerCancellation_PropagatesCancellation()
    {
        var transport = new Mock<IStudywiseTransport>();
        transport.Setup(t => t.SendAsync(It.IsAny<HttpRequestMessage>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());
        var check = new AuthVerifyDiagnosticCheck(transport.Object, new ApplicationConfig { ApiKey = "test-key" });

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var act = () => check.RunAsync(cts.Token);
        await act.Should().ThrowAsync<OperationCanceledException>();
    }
}
