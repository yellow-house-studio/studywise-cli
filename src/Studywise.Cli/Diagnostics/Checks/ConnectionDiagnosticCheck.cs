using System.Net.Http;
using Studywise.Cli.Configuration;

namespace Studywise.Cli.Diagnostics.Checks;

public sealed class ConnectionDiagnosticCheck(IHttpClientFactory httpClientFactory) : IDiagnosticCheck
{
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(5);

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
