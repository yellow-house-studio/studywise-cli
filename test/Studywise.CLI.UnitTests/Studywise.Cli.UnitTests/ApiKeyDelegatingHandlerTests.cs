using System.Net.Http;
using Moq;
using Studywise.Cli.Auth;
using Studywise.Cli.Configuration;

namespace Studywise.CLI.UnitTests;

[Category("Unit")]
public class ApiKeyDelegatingHandlerTests
{
    [Test]
    public async Task SendAsync_AddsApiKeyHeaderWithProviderToken()
    {
        var provider = new Mock<ITokenProvider>();
        provider.Setup(p => p.GetToken()).Returns("test-key");

        HttpRequestMessage? capturedRequest = null;
        var inner = new CapturingHandler(request =>
        {
            capturedRequest = request;
            return new HttpResponseMessage(System.Net.HttpStatusCode.OK);
        });
        var handler = new ApiKeyDelegatingHandler(provider.Object) { InnerHandler = inner };

        using var client = new HttpClient(handler);
        using var response = await client.GetAsync("https://api.studywise.io/anything");

        capturedRequest.Should().NotBeNull();
        capturedRequest!.Headers.GetValues(StudywiseDefaults.ApiKeyHeaderName)
            .Should().ContainSingle().Which.Should().Be("test-key");
        response.StatusCode.Should().Be(System.Net.HttpStatusCode.OK);
    }

    [Test]
    public async Task SendAsync_RemovesPreExistingApiKeyHeaderBeforeAdding()
    {
        var provider = new Mock<ITokenProvider>();
        provider.Setup(p => p.GetToken()).Returns("fresh-key");

        HttpRequestMessage? capturedRequest = null;
        var inner = new CapturingHandler(request =>
        {
            capturedRequest = request;
            return new HttpResponseMessage(System.Net.HttpStatusCode.OK);
        });
        var handler = new ApiKeyDelegatingHandler(provider.Object) { InnerHandler = inner };

        using var client = new HttpClient(handler);
        using var request = new HttpRequestMessage(HttpMethod.Get, "https://api.studywise.io/anything");
        request.Headers.Add(StudywiseDefaults.ApiKeyHeaderName, "stale-key");
        await client.SendAsync(request);

        capturedRequest.Should().NotBeNull();
        capturedRequest!.Headers.GetValues(StudywiseDefaults.ApiKeyHeaderName)
            .Should().ContainSingle().Which.Should().Be("fresh-key");
    }

    [Test]
    public async Task SendAsync_ClearsAuthorizationHeader()
    {
        var provider = new Mock<ITokenProvider>();
        provider.Setup(p => p.GetToken()).Returns("test-key");

        HttpRequestMessage? capturedRequest = null;
        var inner = new CapturingHandler(request =>
        {
            capturedRequest = request;
            return new HttpResponseMessage(System.Net.HttpStatusCode.OK);
        });
        var handler = new ApiKeyDelegatingHandler(provider.Object) { InnerHandler = inner };

        using var client = new HttpClient(handler);
        using var request = new HttpRequestMessage(HttpMethod.Get, "https://api.studywise.io/anything");
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", "some-jwt");
        await client.SendAsync(request);

        capturedRequest.Should().NotBeNull();
        capturedRequest!.Headers.Authorization.Should().BeNull();
        capturedRequest.Headers.GetValues(StudywiseDefaults.ApiKeyHeaderName)
            .Should().ContainSingle().Which.Should().Be("test-key");
    }

    [Test]
    public async Task SendAsync_PropagatesTokenProviderException()
    {
        var provider = new Mock<ITokenProvider>();
        provider.Setup(p => p.GetToken()).Throws(new InvalidOperationException("boom"));

        var inner = new CapturingHandler(_ => new HttpResponseMessage(System.Net.HttpStatusCode.OK));
        var handler = new ApiKeyDelegatingHandler(provider.Object) { InnerHandler = inner };

        using var client = new HttpClient(handler);

        var act = () => client.GetAsync("https://api.studywise.io/anything");

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("boom");
    }

    private sealed class CapturingHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(responder(request));
    }
}
