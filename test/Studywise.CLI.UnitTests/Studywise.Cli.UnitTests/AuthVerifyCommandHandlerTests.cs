using System.Net;
using System.Net.Http;
using System.Text.Json;
using Studywise.Cli.Commands.Auth;
using Studywise.Cli.Configuration;
using Studywise.Contracts.Http.Auth;

namespace Studywise.CLI.UnitTests;

[Category("Unit")]
public class AuthVerifyCommandHandlerTests
{
    [Test]
    public async Task HandleAsync_WithoutJson_WritesTheHumanLineToStdout()
    {
        var stub = StubHttpMessageHandler.Responding(
            HttpStatusCode.OK,
            "{\"userId\":\"users/abc-123\",\"authMethod\":\"ApiKey\"}");
        var handler = BuildHandler(stub, "test-key");
        var console = new TestConsole();

        var exitCode = await handler.HandleAsync(new AuthVerifyCommandOptions(Json: false), console, CancellationToken.None);

        exitCode.Should().Be(0);
        console.Lines.Should().ContainSingle();
        console.Lines[0].Should().Contain("users/abc-123").And.Contain("ApiKey");
        console.ErrorOutput.Should().BeEmpty();
    }

    [Test]
    public async Task HandleAsync_WithJson_WritesTheNormalizedRecordToStdout()
    {
        var stub = StubHttpMessageHandler.Responding(
            HttpStatusCode.OK,
            "{\"userId\":\"users/abc-123\",\"authMethod\":\"ApiKey\"}");
        var handler = BuildHandler(stub, "test-key");
        var console = new TestConsole();

        var exitCode = await handler.HandleAsync(new AuthVerifyCommandOptions(Json: true), console, CancellationToken.None);

        exitCode.Should().Be(0);
        console.Lines.Should().ContainSingle();
        using var json = JsonDocument.Parse(console.Lines[0]);
        json.RootElement.GetProperty("userId").GetString().Should().Be("users/abc-123");
        json.RootElement.GetProperty("authMethod").GetString().Should().Be("ApiKey");
        console.ErrorOutput.Should().BeEmpty();
    }

    [Test]
    public async Task HandleAsync_WithNoApiKey_ReturnsUsageErrorOnStderr()
    {
        var stub = StubHttpMessageHandler.Throwing(new InvalidOperationException("must not be called"));
        var handler = BuildHandler(stub, string.Empty);
        var console = new TestConsole();

        var exitCode = await handler.HandleAsync(new AuthVerifyCommandOptions(Json: false), console, CancellationToken.None);

        exitCode.Should().Be(2);
        console.Lines.Should().BeEmpty();
        console.ErrorOutput.Should().Contain("studywise auth verify:").And.Contain("no API key is configured");
        stub.Requests.Should().BeEmpty();
    }

    [Test]
    public async Task HandleAsync_WithUnauthorized_ReturnsUnauthorizedKind()
    {
        var stub = StubHttpMessageHandler.Responding(HttpStatusCode.Unauthorized, "{\"error\":\"InvalidKey\"}");
        var handler = BuildHandler(stub, "bad-key");
        var console = new TestConsole();

        var exitCode = await handler.HandleAsync(new AuthVerifyCommandOptions(Json: true), console, CancellationToken.None);

        exitCode.Should().Be(1);
        console.Lines.Should().BeEmpty();
        console.ErrorOutput.Should().Contain("HTTP 401 Unauthorized");
        console.ErrorOutput.Should().Contain("\"kind\": \"unauthorized\"");
    }

    [TestCase(HttpStatusCode.Forbidden, "forbidden")]
    [TestCase(HttpStatusCode.NotFound, "notFound")]
    [TestCase(HttpStatusCode.TooManyRequests, "rateLimit")]
    [TestCase(HttpStatusCode.BadRequest, "validation")]
    [TestCase(HttpStatusCode.InternalServerError, "server")]
    public async Task HandleAsync_WithHttpFailure_MapsTheStatusToItsKind(HttpStatusCode status, string expectedKind)
    {
        var stub = StubHttpMessageHandler.Responding(status);
        var handler = BuildHandler(stub, "test-key");
        var console = new TestConsole();

        var exitCode = await handler.HandleAsync(new AuthVerifyCommandOptions(Json: true), console, CancellationToken.None);

        exitCode.Should().Be(1);
        console.ErrorOutput.Should().Contain($"\"kind\": \"{expectedKind}\"");
    }

    [Test]
    public async Task HandleAsync_WithUnreachableApi_ReturnsNetworkKind()
    {
        var stub = StubHttpMessageHandler.Throwing(new HttpRequestException("Name or service not known"));
        var handler = BuildHandler(stub, "test-key");
        var console = new TestConsole();

        var exitCode = await handler.HandleAsync(new AuthVerifyCommandOptions(Json: true), console, CancellationToken.None);

        exitCode.Should().Be(1);
        console.ErrorOutput.Should().Contain("\"kind\": \"network\"");
    }

    [Test]
    public async Task HandleAsync_WithTimeout_ReturnsTimeoutKind()
    {
        var stub = StubHttpMessageHandler.Throwing(new TaskCanceledException("request timed out"));
        var handler = BuildHandler(stub, "test-key");
        var console = new TestConsole();

        var exitCode = await handler.HandleAsync(new AuthVerifyCommandOptions(Json: true), console, CancellationToken.None);

        exitCode.Should().Be(1);
        console.ErrorOutput.Should().Contain("\"kind\": \"timeout\"");
        console.ErrorOutput.Should().Contain("timed out after 30s");
    }

    [Test]
    public async Task HandleAsync_WithCancelledToken_PropagatesCancellation()
    {
        var stub = StubHttpMessageHandler.Throwing(new OperationCanceledException());
        var handler = BuildHandler(stub, "test-key");

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var act = () => handler.HandleAsync(new AuthVerifyCommandOptions(Json: false), new TestConsole(), cts.Token);
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Test]
    public async Task HandleAsync_WithJsonFailure_EmitsBothTheHumanLineAndTheEnvelope()
    {
        var stub = StubHttpMessageHandler.Responding(HttpStatusCode.Unauthorized, "{\"error\":\"InvalidKey\"}");
        var handler = BuildHandler(stub, "bad-key");
        var console = new TestConsole();

        await handler.HandleAsync(new AuthVerifyCommandOptions(Json: true), console, CancellationToken.None);

        console.ErrorOutput.Should().Contain("studywise auth verify: HTTP 401 Unauthorized");
        console.ErrorOutput.Should().Contain("\"exitCode\": 1");
        console.ErrorOutput.Should().Contain("\"path\": \"/api/v1/auth/verify\"");
    }

    private static AuthVerifyCommandHandler BuildHandler(StubHttpMessageHandler stub, string apiKey)
        => new(
            new AuthClient(new HttpClient(stub) { BaseAddress = new Uri("https://api.studywise.test") }),
            new ApplicationConfig { ApiKey = apiKey, ApiBaseUrl = "https://api.studywise.test" });
}
