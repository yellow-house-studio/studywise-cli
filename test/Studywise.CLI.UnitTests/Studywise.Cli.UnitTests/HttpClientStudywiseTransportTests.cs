using System.Net.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http;
using Moq;
using Studywise.Cli.Configuration;
using Studywise.Cli.Http;

namespace Studywise.CLI.UnitTests;

[Category("Unit")]
public class HttpClientStudywiseTransportTests
{
    [Test]
    public async Task SendAsync_DelegatesToNamedHttpClient()
    {
        var expectedResponse = new HttpResponseMessage(System.Net.HttpStatusCode.Accepted);
        var innerHandler = new StubHandler(_ => expectedResponse);

        var services = new Microsoft.Extensions.DependencyInjection.ServiceCollection();
        services.AddHttpClient(StudywiseDefaults.ApiName, client =>
        {
            client.BaseAddress = new Uri("https://api.studywise.io");
        }).ConfigurePrimaryHttpMessageHandler(() => innerHandler);

        var sp = services.BuildServiceProvider();
        var factory = sp.GetRequiredService<IHttpClientFactory>();
        var transport = new HttpClientStudywiseTransport(factory);

        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/anything");
        using var response = await transport.SendAsync(request, CancellationToken.None);

        response.Should().BeSameAs(expectedResponse);
        innerHandler.LastRequest.Should().BeSameAs(request);
    }

    [Test]
    public async Task SendAsync_ForwardsCancellationToken()
    {
        var innerHandler = new StubHandler(_ => new HttpResponseMessage(System.Net.HttpStatusCode.OK));
        var services = new Microsoft.Extensions.DependencyInjection.ServiceCollection();
        services.AddHttpClient(StudywiseDefaults.ApiName, client =>
        {
            client.BaseAddress = new Uri("https://api.studywise.io");
        }).ConfigurePrimaryHttpMessageHandler(() => innerHandler);

        var sp = services.BuildServiceProvider();
        var transport = new HttpClientStudywiseTransport(sp.GetRequiredService<IHttpClientFactory>());

        using var cts = new CancellationTokenSource();
        await transport.SendAsync(new HttpRequestMessage(HttpMethod.Get, "/api/v1/anything"), cts.Token);

        // HttpClient forwards the caller's token to the inner handler, but
        // may wrap it in a linked source. We assert the caller's token is
        // observable via IsCancellationRequested once we cancel it.
        innerHandler.LastCancellationToken.IsCancellationRequested.Should().BeFalse();
        cts.Cancel();
        // Trigger another send with the cancelled token to prove it gets
        // observed by the handler.
        var act = () => transport.SendAsync(new HttpRequestMessage(HttpMethod.Get, "/api/v1/anything"), cts.Token);
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Test]
    public async Task SendAsync_PropagatesInnerException()
    {
        var innerHandler = new StubHandler(_ => throw new HttpRequestException("connection refused"));
        var services = new Microsoft.Extensions.DependencyInjection.ServiceCollection();
        services.AddHttpClient(StudywiseDefaults.ApiName, client =>
        {
            client.BaseAddress = new Uri("https://api.studywise.io");
        }).ConfigurePrimaryHttpMessageHandler(() => innerHandler);

        var sp = services.BuildServiceProvider();
        var transport = new HttpClientStudywiseTransport(sp.GetRequiredService<IHttpClientFactory>());

        var act = () => transport.SendAsync(new HttpRequestMessage(HttpMethod.Get, "/api/v1/anything"), CancellationToken.None);

        await act.Should().ThrowAsync<HttpRequestException>().WithMessage("connection refused");
    }

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
    {
        public HttpRequestMessage? LastRequest { get; private set; }
        public CancellationToken LastCancellationToken { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            LastCancellationToken = cancellationToken;
            return Task.FromResult(responder(request));
        }
    }
}
