namespace Studywise.Cli.Auth;

public sealed class ApiKeyDelegatingHandler(ITokenProvider tokenProvider) : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var token = tokenProvider.GetToken();

        request.Headers.Remove("X-Api-Key");
        request.Headers.Add("X-Api-Key", token);
        request.Headers.Authorization = null;

        return base.SendAsync(request, cancellationToken);
    }
}
