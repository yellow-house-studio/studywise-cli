using System.Net.Http.Headers;

namespace Studywise.Cli.Diagnostics.Checks;

public sealed class ConnectionDiagnosticCheck : IDiagnosticCheck
{
    private const string ApiBaseUrlEnvironmentVariable = "STUDYWISE_API_BASE_URL";
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(5);

    public string Name => "connection";

    public async Task<DiagnosticCheckResult> RunAsync(CancellationToken cancellationToken = default)
    {
        var baseUrl = Environment.GetEnvironmentVariable(ApiBaseUrlEnvironmentVariable) ?? "https://api.studywise.invalid";

        if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var baseUri))
        {
            return new DiagnosticCheckResult(Name, DiagnosticStatus.Warn, $"Connection: WARN — invalid base URL ({baseUrl})");
        }

        using var client = new HttpClient
        {
            BaseAddress = baseUri,
            Timeout = RequestTimeout
        };
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        try
        {
            using var response = await client.GetAsync("/health", cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                return new DiagnosticCheckResult(Name, DiagnosticStatus.Pass, "Connection: OK — /health svarar");
            }

            return new DiagnosticCheckResult(
                Name,
                DiagnosticStatus.Warn,
                $"Connection: WARN — /health svarade med {(int)response.StatusCode}");
        }
        catch (Exception ex)
        {
            return new DiagnosticCheckResult(Name, DiagnosticStatus.Warn, $"Connection: WARN — could not reach /health ({ex.GetType().Name})");
        }
    }
}
