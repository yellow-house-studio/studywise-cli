using System.Net.Http;
using System.Net;
using Moq;
using Studywise.Cli.Diagnostics;
using Studywise.Cli.Diagnostics.Checks;
using Xunit;

namespace Studywise.CLI.UnitTests;

public class ConnectionDiagnosticCheckBoundaryTests
{
    private static IHttpClientFactory CreateMockFactory(HttpClient httpClient)
    {
        var mockFactory = new Mock<IHttpClientFactory>();
        mockFactory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(httpClient);
        return mockFactory.Object;
    }

    [Fact]
    public async Task RunAsync_WithSuccessStatus_ReturnsPass()
    {
        using var httpClient = new HttpClient(new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)))
        {
            BaseAddress = new Uri("https://api.studywise.io")
        };
        var factory = CreateMockFactory(httpClient);
        var check = new ConnectionDiagnosticCheck(factory);

        var result = await check.RunAsync();

        Assert.Equal("connection", result.Name);
        Assert.Equal(DiagnosticStatus.Pass, result.Status);
        Assert.Contains("Connection: OK", result.Message);
    }

    [Theory]
    [InlineData(HttpStatusCode.NotFound, 404)]
    [InlineData(HttpStatusCode.ServiceUnavailable, 503)]
    public async Task RunAsync_WithNonSuccessStatus_ReturnsFail(HttpStatusCode statusCode, int expectedCode)
    {
        using var httpClient = new HttpClient(new StubHttpMessageHandler(_ => new HttpResponseMessage(statusCode)))
        {
            BaseAddress = new Uri("https://api.studywise.io")
        };
        var factory = CreateMockFactory(httpClient);
        var check = new ConnectionDiagnosticCheck(factory);

        var result = await check.RunAsync();

        Assert.Equal("connection", result.Name);
        Assert.Equal(DiagnosticStatus.Fail, result.Status);
        Assert.Contains($"/health returned {expectedCode}", result.Message);
    }

    [Fact]
    public async Task RunAsync_WithRequestTimeout_ReturnsFail()
    {
        using var httpClient = new HttpClient(new StubHttpMessageHandler(_ => throw new TaskCanceledException("request timed out")))
        {
            BaseAddress = new Uri("https://api.studywise.io")
        };
        var factory = CreateMockFactory(httpClient);
        var check = new ConnectionDiagnosticCheck(factory);

        var result = await check.RunAsync();

        Assert.Equal("connection", result.Name);
        Assert.Equal(DiagnosticStatus.Fail, result.Status);
        Assert.Contains("timeout after 5s", result.Message);
    }

    [Fact]
    public async Task RunAsync_WithUnreachableHost_ReturnsFail()
    {
        using var httpClient = new HttpClient(new StubHttpMessageHandler(_ => throw new HttpRequestException("Name or service not known")))
        {
            BaseAddress = new Uri("https://api.studywise.io")
        };
        var factory = CreateMockFactory(httpClient);
        var check = new ConnectionDiagnosticCheck(factory);

        var result = await check.RunAsync();

        Assert.Equal("connection", result.Name);
        Assert.Equal(DiagnosticStatus.Fail, result.Status);
        Assert.Contains("could not reach /health", result.Message);
        Assert.Contains("HttpRequestException", result.Message);
    }

    [Fact]
    public async Task RunAsync_WithCancelledToken_PropagatesCancellation()
    {
        using var httpClient = new HttpClient { BaseAddress = new Uri("http://127.0.0.1:65535") };
        var factory = CreateMockFactory(httpClient);

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var check = new ConnectionDiagnosticCheck(factory);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => check.RunAsync(cts.Token));
    }

    private sealed class StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(responder(request));
    }
}
