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
    public async Task RunAsync_WithCancelledToken_ReturnsWarnResult()
    {
        using var httpClient = new HttpClient { BaseAddress = new Uri("http://127.0.0.1:65535") };
        var factory = CreateMockFactory(httpClient);

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var check = new ConnectionDiagnosticCheck(factory);

        var result = await check.RunAsync(cts.Token);

        Assert.Equal("connection", result.Name);
        Assert.Equal(DiagnosticStatus.Warn, result.Status);
        Assert.Contains("could not reach /health", result.Message);
        Assert.Contains("TaskCanceledException", result.Message);
    }

    [Fact]
    public async Task RunAsync_WithUnauthorizedResponse_ReturnsApiKeyInvalidMessage()
    {
        using var httpClient = new HttpClient(new StaticResponseHandler(HttpStatusCode.Unauthorized))
        {
            BaseAddress = new Uri("https://example.com")
        };

        var check = new ConnectionDiagnosticCheck(CreateMockFactory(httpClient));
        var result = await check.RunAsync();

        Assert.Equal(DiagnosticStatus.Fail, result.Status);
        Assert.Equal("API-nyckel ogiltig eller återkallad. Kontrollera STUDYWISE_API_KEY.", result.Message);
    }

    [Fact]
    public async Task RunAsync_WithForbiddenResponse_ReturnsWrongFamilyMessage()
    {
        using var httpClient = new HttpClient(new StaticResponseHandler(HttpStatusCode.Forbidden))
        {
            BaseAddress = new Uri("https://example.com")
        };

        var check = new ConnectionDiagnosticCheck(CreateMockFactory(httpClient));
        var result = await check.RunAsync();

        Assert.Equal(DiagnosticStatus.Fail, result.Status);
        Assert.Equal("API-nyckel inte giltig för denna familj. Kontrollera STUDYWISE_API_KEY.", result.Message);
    }

    [Fact]
    public async Task RunAsync_WithMissingApiKeyFailure_ReturnsMissingKeyMessage()
    {
        using var httpClient = new HttpClient(new ThrowingHandler(new InvalidOperationException("API-nyckel saknas. Sätt STUDYWISE_API_KEY.")))
        {
            BaseAddress = new Uri("https://example.com")
        };

        var check = new ConnectionDiagnosticCheck(CreateMockFactory(httpClient));
        var result = await check.RunAsync();

        Assert.Equal(DiagnosticStatus.Fail, result.Status);
        Assert.Equal("API-nyckel saknas. Sätt STUDYWISE_API_KEY.", result.Message);
    }

    private sealed class StaticResponseHandler(HttpStatusCode statusCode) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(new HttpResponseMessage(statusCode));
    }

    private sealed class ThrowingHandler(Exception exception) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromException<HttpResponseMessage>(exception);
    }
}
