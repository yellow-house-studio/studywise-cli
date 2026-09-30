using System.Net;
using System.Net.Http;
using Moq;
using Studywise.Cli.Diagnostics;
using Studywise.Cli.Diagnostics.Checks;

namespace Studywise.CLI.UnitTests;

[Category("Unit")]
public class ConnectionDiagnosticCheckBoundaryTests
{
    private static IHttpClientFactory CreateMockFactory(HttpClient httpClient)
    {
        var mockFactory = new Mock<IHttpClientFactory>();
        mockFactory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(httpClient);
        return mockFactory.Object;
    }

    [Test]
    public async Task RunAsync_WithSuccessStatus_ReturnsPass()
    {
        using var httpClient = new HttpClient(new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)))
        {
            BaseAddress = new Uri("https://api.studywise.io")
        };
        var factory = CreateMockFactory(httpClient);
        var check = new ConnectionDiagnosticCheck(factory);

        var result = await check.RunAsync();

        result.Name.Should().Be("connection");
        result.Status.Should().Be(DiagnosticStatus.Pass);
        result.Message.Should().Contain("Connection: OK");
    }

    [TestCase(HttpStatusCode.NotFound, 404)]
    [TestCase(HttpStatusCode.ServiceUnavailable, 503)]
    public async Task RunAsync_WithNonSuccessStatus_ReturnsFail(HttpStatusCode statusCode, int expectedCode)
    {
        using var httpClient = new HttpClient(new StubHttpMessageHandler(_ => new HttpResponseMessage(statusCode)))
        {
            BaseAddress = new Uri("https://api.studywise.io")
        };
        var factory = CreateMockFactory(httpClient);
        var check = new ConnectionDiagnosticCheck(factory);

        var result = await check.RunAsync();

        result.Name.Should().Be("connection");
        result.Status.Should().Be(DiagnosticStatus.Fail);
        result.Message.Should().Contain($"/health returned {expectedCode}");
    }

    [Test]
    public async Task RunAsync_WithRequestTimeout_ReturnsFail()
    {
        using var httpClient = new HttpClient(new StubHttpMessageHandler(_ => throw new TaskCanceledException("request timed out")))
        {
            BaseAddress = new Uri("https://api.studywise.io")
        };
        var factory = CreateMockFactory(httpClient);
        var check = new ConnectionDiagnosticCheck(factory);

        var result = await check.RunAsync();

        result.Name.Should().Be("connection");
        result.Status.Should().Be(DiagnosticStatus.Fail);
        result.Message.Should().Contain("timeout after 5s");
    }

    [Test]
    public async Task RunAsync_WithUnreachableHost_ReturnsFail()
    {
        using var httpClient = new HttpClient(new StubHttpMessageHandler(_ => throw new HttpRequestException("Name or service not known")))
        {
            BaseAddress = new Uri("https://api.studywise.io")
        };
        var factory = CreateMockFactory(httpClient);
        var check = new ConnectionDiagnosticCheck(factory);

        var result = await check.RunAsync();

        result.Name.Should().Be("connection");
        result.Status.Should().Be(DiagnosticStatus.Fail);
        result.Message.Should().Contain("could not reach /health");
        result.Message.Should().Contain("HttpRequestException");
    }

    [Test]
    public async Task RunAsync_WithCancelledToken_PropagatesCancellation()
    {
        using var httpClient = new HttpClient { BaseAddress = new Uri("http://127.0.0.1:65535") };
        var factory = CreateMockFactory(httpClient);

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var check = new ConnectionDiagnosticCheck(factory);

        Func<Task> act = () => check.RunAsync(cts.Token);
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    private sealed class StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(responder(request));
    }
}
