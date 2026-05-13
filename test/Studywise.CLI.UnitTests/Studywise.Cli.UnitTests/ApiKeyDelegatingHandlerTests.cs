using System.Net;
using System.Net.Http;
using Studywise.Cli.Auth;

namespace Studywise.Cli.UnitTests;

public class ApiKeyDelegatingHandlerTests
{
    [Fact]
    public async Task SendAsync_AddsXApiKeyHeaderFromTokenProvider()
    {
        var captureHandler = new CaptureHandler();
        var handler = new ApiKeyDelegatingHandler(new ApiKeyTokenProvider("sk_test_9876"))
        {
            InnerHandler = captureHandler
        };

        using var client = new HttpClient(handler);
        _ = await client.GetAsync("https://example.com/health");

        Assert.NotNull(captureHandler.Request);
        Assert.True(captureHandler.Request!.Headers.TryGetValues("X-Api-Key", out var values));
        Assert.Equal("sk_test_9876", values.Single());
    }

    private sealed class CaptureHandler : HttpMessageHandler
    {
        public HttpRequestMessage? Request { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Request = request;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        }
    }
}
