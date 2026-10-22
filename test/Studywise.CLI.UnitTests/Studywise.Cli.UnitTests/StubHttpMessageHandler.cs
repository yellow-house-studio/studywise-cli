using System.Net;
using System.Text;

namespace Studywise.CLI.UnitTests;

/// <summary>
/// Minimal <see cref="HttpMessageHandler"/> stub used to drive the SDK's typed clients
/// in unit tests. The SDK's clients are concrete classes wrapping an
/// <see cref="HttpClient"/>, so the seam is the handler rather than a mocked interface
/// (docs/design/cli-design.md § 6).
/// </summary>
internal sealed class StubHttpMessageHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _handler;

    public StubHttpMessageHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler)
        => _handler = handler;

    /// <summary>Requests the stub has seen, in order.</summary>
    public List<HttpRequestMessage> Requests { get; } = [];

    /// <summary>
    /// Builds a stub that answers every request with the given status code and a JSON body.
    /// </summary>
    public static StubHttpMessageHandler Responding(HttpStatusCode statusCode, string body = "{}")
        => new((_, _) => Task.FromResult(new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        }));

    /// <summary>
    /// Builds a stub that throws the given exception for every request.
    /// </summary>
    public static StubHttpMessageHandler Throwing(Exception exception)
        => new((_, _) => throw exception);

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Requests.Add(request);
        return _handler(request, cancellationToken);
    }
}
