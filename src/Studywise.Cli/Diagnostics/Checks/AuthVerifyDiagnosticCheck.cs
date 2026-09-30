using System.Net.Http;
using System.Text.Json;
using Studywise.Cli.Configuration;
using Studywise.Cli.Http;

namespace Studywise.Cli.Diagnostics.Checks;

public sealed class AuthVerifyDiagnosticCheck(
    IStudywiseTransport transport,
    ApplicationConfig config) : IDiagnosticCheck
{
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(5);
    private const int TimeoutSeconds = 5;

    public string Name => "auth-verify";

    public async Task<DiagnosticCheckResult> RunAsync(CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(config.ApiKey))
        {
            return new DiagnosticCheckResult(
                Name,
                DiagnosticStatus.Warn,
                "Auth verify: SKIP — no API key configured");
        }

        using var request = new HttpRequestMessage(HttpMethod.Get, StudywiseDefaults.AuthVerifyPath);

        try
        {
            using var response = await transport.SendAsync(request, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(cancellationToken);
                var (userId, authMethod) = ParseAuthVerification(body);
                var userIdDisplay = string.IsNullOrEmpty(userId) ? "unknown" : userId;
                var authMethodDisplay = string.IsNullOrEmpty(authMethod) ? "unknown" : authMethod;
                return new DiagnosticCheckResult(
                    Name,
                    DiagnosticStatus.Pass,
                    $"Auth verify: OK — connected as {userIdDisplay} ({authMethodDisplay})");
            }

            if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                return new DiagnosticCheckResult(
                    Name,
                    DiagnosticStatus.Fail,
                    "Auth verify: FAIL — invalid or revoked API key");
            }

            return new DiagnosticCheckResult(
                Name,
                DiagnosticStatus.Fail,
                $"Auth verify: FAIL — server returned {(int)response.StatusCode}");
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
                $"Auth verify: FAIL — timeout after {TimeoutSeconds}s reaching {StudywiseDefaults.AuthVerifyPath}");
        }
        catch (HttpRequestException ex)
        {
            return new DiagnosticCheckResult(
                Name,
                DiagnosticStatus.Fail,
                $"Auth verify: FAIL — could not reach {StudywiseDefaults.AuthVerifyPath} ({ex.GetType().Name})");
        }
        catch (Exception ex)
        {
            return new DiagnosticCheckResult(
                Name,
                DiagnosticStatus.Fail,
                $"Auth verify: FAIL — could not reach {StudywiseDefaults.AuthVerifyPath} ({ex.GetType().Name})");
        }
    }

    private static (string UserId, string AuthMethod) ParseAuthVerification(string body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return (string.Empty, string.Empty);
        }

        try
        {
            using var document = JsonDocument.Parse(body);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return (string.Empty, string.Empty);
            }

            string userId = string.Empty;
            string authMethod = string.Empty;

            if (document.RootElement.TryGetProperty("userId", out var userIdElement) &&
                userIdElement.ValueKind == JsonValueKind.String)
            {
                userId = userIdElement.GetString() ?? string.Empty;
            }

            if (document.RootElement.TryGetProperty("authMethod", out var authMethodElement) &&
                authMethodElement.ValueKind == JsonValueKind.String)
            {
                authMethod = authMethodElement.GetString() ?? string.Empty;
            }

            return (userId, authMethod);
        }
        catch (JsonException)
        {
            return (string.Empty, string.Empty);
        }
    }
}
