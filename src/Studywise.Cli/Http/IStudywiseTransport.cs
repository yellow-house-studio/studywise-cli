namespace Studywise.Cli.Http;

public interface IStudywiseTransport
{
    Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken);
}
