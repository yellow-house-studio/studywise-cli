using System.Net.Http;

namespace Studywise.Cli.Diagnostics.Checks;

public sealed class ConnectionDiagnosticCheck(HttpClient httpClient) : IDiagnosticCheck
{
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(5);

    public string Name => "connection";

    public async Task<DiagnosticCheckResult> RunAsync(CancellationToken cancellationToken = default)
    {
        httpClient.Timeout = RequestTimeout;

        try
        {
            using var response = await httpClient.GetAsync("/health", cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                return new DiagnosticCheckResult(Name, DiagnosticStatus.Pass, "Connection: OK — /health responded");
            }

            return new DiagnosticCheckResult(
                Name,
                DiagnosticStatus.Warn,
                $"Connection: WARN — /health returned {(int)response.StatusCode}");
        }
        catch (Exception ex)
        {
            return new DiagnosticCheckResult(Name, DiagnosticStatus.Warn, $"Connection: WARN — could not reach /health ({ex.GetType().Name})");
        }
    }
}
