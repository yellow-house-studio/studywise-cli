namespace Studywise.Cli.Http;

/// <summary>
/// Single seam through which CLI code sends HTTP requests to the Studywise API.
/// Today's implementation (<see cref="HttpClientStudywiseTransport"/>) wraps a named
/// <see cref="HttpClient"/>; future implementations can back onto a typed SDK
/// without changes to callers.
/// </summary>
public interface IStudywiseTransport
{
    /// <summary>
    /// Sends an HTTP request and returns the response.
    /// </summary>
    /// <param name="request">The request to send. The transport may mutate headers (auth) before sending.</param>
    /// <param name="cancellationToken">Token observed for cancellation.</param>
    /// <returns>The HTTP response.</returns>
    Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken);
}
