using Studywise.Cli.Configuration;

namespace Studywise.Cli.Http;

/// <summary>
/// <see cref="IStudywiseTransport"/> backed by a named <see cref="HttpClient"/> resolved
/// from <see cref="IHttpClientFactory"/>. The named client is configured at startup with
/// <see cref="Auth.ApiKeyDelegatingHandler"/> in its message-handler pipeline, so requests
/// arrive at the API with the API key header already set.
/// </summary>
/// <param name="httpClientFactory">Factory that resolves the named API client.</param>
public sealed class HttpClientStudywiseTransport(IHttpClientFactory httpClientFactory) : IStudywiseTransport
{
    /// <summary>
    /// Resolves the named client and forwards the request to it.
    /// </summary>
    /// <param name="request">The request to send.</param>
    /// <param name="cancellationToken">Token observed for cancellation.</param>
    /// <returns>The HTTP response.</returns>
    public Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var client = httpClientFactory.CreateClient(StudywiseDefaults.ApiName);
        return client.SendAsync(request, cancellationToken);
    }
}
