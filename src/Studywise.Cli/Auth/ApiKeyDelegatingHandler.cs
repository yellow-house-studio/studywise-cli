using Studywise.Cli.Configuration;

namespace Studywise.Cli.Auth;

/// <summary>
/// <see cref="DelegatingHandler"/> that attaches <see cref="StudywiseDefaults.ApiKeyHeaderName"/>
/// to every outbound request, using the credential returned by the supplied <see cref="ITokenProvider"/>.
/// </summary>
/// <param name="tokenProvider">Source of the credential to attach to each request.</param>
public sealed class ApiKeyDelegatingHandler(ITokenProvider tokenProvider) : DelegatingHandler
{
    /// <summary>
    /// Resolves the credential, sets the API key header, and forwards to the inner handler.
    /// </summary>
    /// <param name="request">The outbound request being sent.</param>
    /// <param name="cancellationToken">Token observed for cancellation.</param>
    /// <returns>The HTTP response from the inner handler.</returns>
    /// <exception cref="System.InvalidOperationException">
    /// Thrown by the token provider when no credential is configured.
    /// </exception>
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var token = tokenProvider.GetToken();

        // Remove any pre-existing header so the request never carries both stale and fresh values
        // (e.g. when a request is retried or cloned). Clearing Authorization prevents the API key
        // header from being sent alongside a Bearer token from a different auth scheme.
        request.Headers.Remove(StudywiseDefaults.ApiKeyHeaderName);
        request.Headers.Add(StudywiseDefaults.ApiKeyHeaderName, token);
        request.Headers.Authorization = null;

        return base.SendAsync(request, cancellationToken);
    }
}
