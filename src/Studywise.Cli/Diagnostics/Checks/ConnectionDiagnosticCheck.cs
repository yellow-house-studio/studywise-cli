using System.Net.Http;
using Studywise.Cli.Configuration;

namespace Studywise.Cli.Diagnostics.Checks;

/// <summary>
/// Diagnostic check that probes the Studywise API's liveness endpoint and reports
/// whether the API is reachable. Does not exercise authentication.
/// </summary>
/// <remarks>
/// The API maps <c>/health/live</c> and <c>/health/ready</c>. There is no bare
/// <c>/health</c> route, so a request to one 404s. This check uses the liveness
/// endpoint: it reports whether the process is up, without depending on the
/// readiness checks or on any downstream service.
/// </remarks>
/// <param name="httpClientFactory">Factory that resolves the named API HttpClient.</param>
public sealed class ConnectionDiagnosticCheck(IHttpClientFactory httpClientFactory) : IDiagnosticCheck
{
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(5);
    private const int TimeoutSeconds = 5;

    /// <inheritdoc />
    public string Name => "connection";

    /// <summary>
    /// Probes the API's liveness endpoint with a 5-second timeout.
    /// </summary>
    /// <param name="cancellationToken">Token observed for cancellation.</param>
    /// <returns>The check outcome.</returns>
    public async Task<DiagnosticCheckResult> RunAsync(CancellationToken cancellationToken = default)
    {
        using var client = httpClientFactory.CreateClient(StudywiseDefaults.ApiName);
        client.Timeout = RequestTimeout;

        try
        {
            using var response = await client.GetAsync(StudywiseDefaults.HealthPath, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                return new DiagnosticCheckResult(
                    Name,
                    DiagnosticStatus.Pass,
                    $"Connection: OK — {StudywiseDefaults.HealthPath} responded");
            }

            return new DiagnosticCheckResult(
                Name,
                DiagnosticStatus.Fail,
                $"Connection: FAIL — {StudywiseDefaults.HealthPath} returned {(int)response.StatusCode}");
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
                $"Connection: FAIL — timeout after {TimeoutSeconds}s reaching {StudywiseDefaults.HealthPath}");
        }
        catch (HttpRequestException ex)
        {
            return new DiagnosticCheckResult(
                Name,
                DiagnosticStatus.Fail,
                $"Connection: FAIL — could not reach {StudywiseDefaults.HealthPath} ({ex.GetType().Name})");
        }
        catch (Exception ex)
        {
            return new DiagnosticCheckResult(
                Name,
                DiagnosticStatus.Fail,
                $"Connection: FAIL — could not reach {StudywiseDefaults.HealthPath} ({ex.GetType().Name})");
        }
    }
}
