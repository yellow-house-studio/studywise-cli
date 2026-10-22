using System.Net;
using System.Net.Http;
using Studywise.Cli.Configuration;
using Studywise.Cli.Diagnostics;
using Studywise.Cli.Diagnostics.Checks;
using Studywise.Contracts.Http.Auth;

namespace Studywise.CLI.UnitTests;

[Category("Unit")]
public class AuthVerifyDiagnosticCheckTests
{
    [Test]
    public async Task RunAsync_NoKeyConfigured_ReturnsWarnSkip()
    {
        var stub = StubHttpMessageHandler.Throwing(new InvalidOperationException("must not be called"));
        var check = new AuthVerifyDiagnosticCheck(BuildClient(stub), new ApplicationConfig { ApiKey = "" });

        var result = await check.RunAsync();

        result.Name.Should().Be("auth-verify");
        result.Status.Should().Be(DiagnosticStatus.Warn);
        result.Message.Should().Contain("SKIP").And.Contain("no API key configured");
        stub.Requests.Should().BeEmpty();
    }

    [Test]
    public async Task RunAsync_KeyWhitespaceOnly_ReturnsWarnSkip()
    {
        var stub = StubHttpMessageHandler.Throwing(new InvalidOperationException("must not be called"));
        var check = new AuthVerifyDiagnosticCheck(BuildClient(stub), new ApplicationConfig { ApiKey = "   " });

        var result = await check.RunAsync();

        result.Status.Should().Be(DiagnosticStatus.Warn);
        result.Message.Should().Contain("SKIP");
        stub.Requests.Should().BeEmpty();
    }

    [Test]
    public async Task RunAsync_Success200_ReturnsPassWithUserAndMethod()
    {
        var stub = StubHttpMessageHandler.Responding(
            HttpStatusCode.OK,
            "{\"userId\":\"users/abc-123\",\"authMethod\":\"ApiKey\"}");
        var check = new AuthVerifyDiagnosticCheck(BuildClient(stub), new ApplicationConfig { ApiKey = "test-key" });

        var result = await check.RunAsync();

        result.Status.Should().Be(DiagnosticStatus.Pass);
        result.Message.Should().Contain("users/abc-123");
        result.Message.Should().Contain("ApiKey");
    }

    [Test]
    public async Task RunAsync_Unauthorized_ReturnsFailInvalidOrRevoked()
    {
        var stub = StubHttpMessageHandler.Responding(HttpStatusCode.Unauthorized, "{\"error\":\"InvalidKey\"}");
        var check = new AuthVerifyDiagnosticCheck(BuildClient(stub), new ApplicationConfig { ApiKey = "bad-key" });

        var result = await check.RunAsync();

        result.Status.Should().Be(DiagnosticStatus.Fail);
        result.Message.Should().Contain("invalid or revoked");
    }

    [Test]
    public async Task RunAsync_UnauthorizedWithEmptyBody_ReturnsFailInvalidOrRevoked()
    {
        var stub = StubHttpMessageHandler.Responding(HttpStatusCode.Unauthorized, string.Empty);
        var check = new AuthVerifyDiagnosticCheck(BuildClient(stub), new ApplicationConfig { ApiKey = "bad-key" });

        var result = await check.RunAsync();

        result.Status.Should().Be(DiagnosticStatus.Fail);
        result.Message.Should().Contain("invalid or revoked");
    }

    [TestCase(HttpStatusCode.InternalServerError, 500)]
    [TestCase(HttpStatusCode.ServiceUnavailable, 503)]
    [TestCase(HttpStatusCode.BadGateway, 502)]
    public async Task RunAsync_NonSuccessNonUnauthorizedStatus_ReturnsFailWithStatusCode(HttpStatusCode status, int expectedCode)
    {
        var stub = StubHttpMessageHandler.Responding(status);
        var check = new AuthVerifyDiagnosticCheck(BuildClient(stub), new ApplicationConfig { ApiKey = "test-key" });

        var result = await check.RunAsync();

        result.Status.Should().Be(DiagnosticStatus.Fail);
        result.Message.Should().Contain($"server returned {expectedCode}");
    }

    [Test]
    public async Task RunAsync_RequestTimeout_ReturnsFailWithTimeoutMessage()
    {
        var stub = StubHttpMessageHandler.Throwing(new TaskCanceledException("request timed out"));
        var check = new AuthVerifyDiagnosticCheck(BuildClient(stub), new ApplicationConfig { ApiKey = "test-key" });

        var result = await check.RunAsync();

        result.Status.Should().Be(DiagnosticStatus.Fail);
        result.Message.Should().Contain("timeout after 5s");
    }

    [Test]
    public async Task RunAsync_UnreachableHost_ReturnsFailWithUnreachableMessage()
    {
        var stub = StubHttpMessageHandler.Throwing(new HttpRequestException("Name or service not known"));
        var check = new AuthVerifyDiagnosticCheck(BuildClient(stub), new ApplicationConfig { ApiKey = "test-key" });

        var result = await check.RunAsync();

        result.Status.Should().Be(DiagnosticStatus.Fail);
        result.Message.Should().Contain("could not reach");
        result.Message.Should().Contain("HttpRequestException");
    }

    [Test]
    public async Task RunAsync_MalformedBody_ReturnsFailInsteadOfThrowing()
    {
        var stub = StubHttpMessageHandler.Responding(HttpStatusCode.OK, "not json at all");
        var check = new AuthVerifyDiagnosticCheck(BuildClient(stub), new ApplicationConfig { ApiKey = "test-key" });

        var result = await check.RunAsync();

        result.Status.Should().Be(DiagnosticStatus.Fail);
        result.Message.Should().Contain("the request failed");
    }

    [Test]
    public async Task RunAsync_CallerCancellation_PropagatesCancellation()
    {
        var stub = StubHttpMessageHandler.Throwing(new OperationCanceledException());
        var check = new AuthVerifyDiagnosticCheck(BuildClient(stub), new ApplicationConfig { ApiKey = "test-key" });

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var act = () => check.RunAsync(cts.Token);
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Test]
    public async Task RunAsync_RequestsTheSdkAuthVerifyRoute()
    {
        var stub = StubHttpMessageHandler.Responding(
            HttpStatusCode.OK,
            "{\"userId\":\"users/abc-123\",\"authMethod\":\"ApiKey\"}");
        var check = new AuthVerifyDiagnosticCheck(BuildClient(stub), new ApplicationConfig { ApiKey = "test-key" });

        await check.RunAsync();

        var request = stub.Requests.Should().ContainSingle().Subject;
        request.Method.Should().Be(HttpMethod.Get);
        request.RequestUri.Should().Be(new Uri("https://api.studywise.test/api/v1/auth/verify"));
    }

    private static AuthClient BuildClient(StubHttpMessageHandler stub)
        => new(new HttpClient(stub) { BaseAddress = new Uri("https://api.studywise.test") });
}
