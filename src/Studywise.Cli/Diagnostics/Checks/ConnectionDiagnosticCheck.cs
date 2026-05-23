using System.Net.Http;
using System.Net;
using Studywise.Cli.Configuration;

namespace Studywise.Cli.Diagnostics.Checks;

public sealed class ConnectionDiagnosticCheck(IHttpClientFactory httpClientFactory) : IDiagnosticCheck
{
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(5);
    private const int TimeoutSeconds = 5;

    public string Name => "connection";

    public async Task<DiagnosticCheckResult> RunAsync(CancellationToken cancellationToken = default)
    {
        using var client = httpClientFactory.CreateClient(StudywiseDefaults.ApiName);
        client.Timeout = RequestTimeout;

        try
        {
            using var response = await client.GetAsync("/health", cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                return new DiagnosticCheckResult(Name, DiagnosticStatus.Pass, "Connection: OK — /health responded");
            }

            var message = response.StatusCode switch
            {
                HttpStatusCode.Unauthorized => "Connection: FAIL — API-nyckel ogiltig eller återkallad. Kontrollera STUDYWISE_API_KEY.",
                HttpStatusCode.Forbidden => "Connection: FAIL — API-nyckel inte giltig för denna familj. Kontrollera STUDYWISE_API_KEY.",
                _ => $"Connection: FAIL — /health returned {(int)response.StatusCode}"
            };

            return new DiagnosticCheckResult(
                Name,
                DiagnosticStatus.Fail,
                message);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (TaskCanceledException)
        {
            return new DiagnosticCheckResult(
                Name,
                DiagnosticStatus.Fail,
                $"Connection: FAIL — timeout after {TimeoutSeconds}s reaching /health");
        }
        catch (HttpRequestException ex)
        {
            return new DiagnosticCheckResult(
                Name,
                DiagnosticStatus.Fail,
                $"Connection: FAIL — could not reach /health ({ex.GetType().Name})");
        }
        catch (Exception ex)
        {
            return new DiagnosticCheckResult(
                Name,
                DiagnosticStatus.Fail,
                $"Connection: FAIL — could not reach /health ({ex.GetType().Name})");
        }
    }
}
