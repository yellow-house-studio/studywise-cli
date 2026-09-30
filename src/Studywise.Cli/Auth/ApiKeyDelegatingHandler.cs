using Studywise.Cli.Configuration;

namespace Studywise.Cli.Auth;

public sealed class ApiKeyDelegatingHandler(ITokenProvider tokenProvider) : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var token = tokenProvider.GetToken();

        request.Headers.Remove(StudywiseDefaults.ApiKeyHeaderName);
        request.Headers.Add(StudywiseDefaults.ApiKeyHeaderName, token);
        request.Headers.Authorization = null;

        return base.SendAsync(request, cancellationToken);
    }
}
