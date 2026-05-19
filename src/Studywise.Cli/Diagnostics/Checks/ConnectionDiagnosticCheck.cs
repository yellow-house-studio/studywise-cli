using System.Net.Http;
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
            using var response = await client.GetAsync("/api/families", cancellationToken);
            if ((int)response.StatusCode == 401)
            {
                return new DiagnosticCheckResult(Name, DiagnosticStatus.Fail, "API-nyckel ogiltig eller återkallad. Kontrollera STUDYWISE_API_KEY.");
            }

            if ((int)response.StatusCode == 403)
            {
                return new DiagnosticCheckResult(Name, DiagnosticStatus.Fail, "API-nyckel inte giltig för denna familj. Kontrollera STUDYWISE_API_KEY.");
            }

            if (response.IsSuccessStatusCode)
            {
                return new DiagnosticCheckResult(Name, DiagnosticStatus.Pass, "Connection: OK — /api/families responded");
            }

            return new DiagnosticCheckResult(
                Name,
                DiagnosticStatus.Fail,
                $"Connection: FAIL — /api/families returned {(int)response.StatusCode}");
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
                $"Connection: FAIL — timeout after {TimeoutSeconds}s reaching /api/families");
        }
        catch (HttpRequestException ex)
        {
            return new DiagnosticCheckResult(
                Name,
                DiagnosticStatus.Fail,
                $"Connection: FAIL — could not reach /api/families ({ex.GetType().Name})");
        }
        catch (InvalidOperationException ex) when (ex.Message == "API-nyckel saknas. Sätt STUDYWISE_API_KEY.")
        {
            return new DiagnosticCheckResult(Name, DiagnosticStatus.Fail, ex.Message);
        }
        catch (Exception ex)
        {
            return new DiagnosticCheckResult(
                Name,
                DiagnosticStatus.Fail,
                $"Connection: FAIL — could not reach /api/families ({ex.GetType().Name})");
        }
    }
}
