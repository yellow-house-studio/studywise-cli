using Studywise.Cli.Configuration;

namespace Studywise.Cli.Http;

public sealed class HttpClientStudywiseTransport(IHttpClientFactory httpClientFactory) : IStudywiseTransport
{
    public Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var client = httpClientFactory.CreateClient(StudywiseDefaults.ApiName);
        return client.SendAsync(request, cancellationToken);
    }
}
