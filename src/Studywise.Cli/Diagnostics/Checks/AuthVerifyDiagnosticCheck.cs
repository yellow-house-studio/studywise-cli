using System.Net;
using Studywise.Cli.Configuration;
using Studywise.Contracts.Http.Auth;

namespace Studywise.Cli.Diagnostics.Checks;

/// <summary>
/// Diagnostic check that verifies the configured API key against the Studywise API and
/// reports whether it authenticates successfully.
/// <para>
/// Backed by the SDK's <see cref="AuthClient"/>, so the endpoint and the
/// <c>X-Studywise-Api-Key</c> header are the SDK's concern rather than the CLI's.
/// </para>
/// <para>
/// Returns <see cref="DiagnosticStatus.Warn"/> (SKIP) when no API key is configured.
/// </para>
/// </summary>
/// <param name="authClient">SDK client used to verify the configured credentials.</param>
/// <param name="config">Resolved CLI configuration (the API key is read from here).</param>
public sealed class AuthVerifyDiagnosticCheck(
    AuthClient authClient,
    ApplicationConfig config) : IDiagnosticCheck
{
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(5);

    /// <inheritdoc />
    public string Name => "auth-verify";

    /// <summary>
    /// Runs the check.
    /// </summary>
    /// <param name="cancellationToken">Token observed for cancellation.</param>
    /// <returns>The check outcome.</returns>
    public async Task<DiagnosticCheckResult> RunAsync(CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(config.ApiKey))
        {
            return new DiagnosticCheckResult(
                Name,
                DiagnosticStatus.Warn,
                "Auth verify: SKIP — no API key configured");
        }

        // The SDK's client carries a 30 second HttpClient timeout. The doctor check
        // wants a tighter bound, so it applies its own via a linked token rather
        // than reconfiguring the SDK's client.
        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(RequestTimeout);

        try
        {
            var verification = await authClient.VerifyAsync(timeoutSource.Token);
            var userIdDisplay = string.IsNullOrEmpty(verification.UserId) ? "unknown" : verification.UserId;
            var authMethodDisplay = string.IsNullOrEmpty(verification.AuthMethod) ? "unknown" : verification.AuthMethod;
            return new DiagnosticCheckResult(
                Name,
                DiagnosticStatus.Pass,
                $"Auth verify: OK — connected as {userIdDisplay} ({authMethodDisplay})");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Caller-cancelled - rethrow so callers can observe it. The remaining
            // catches translate transport-level timeouts into a FAIL result.
            throw;
        }
        catch (OperationCanceledException)
        {
            return new DiagnosticCheckResult(
                Name,
                DiagnosticStatus.Fail,
                $"Auth verify: FAIL — timeout after {(int)RequestTimeout.TotalSeconds}s");
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.Unauthorized)
        {
            return new DiagnosticCheckResult(
                Name,
                DiagnosticStatus.Fail,
                "Auth verify: FAIL — invalid or revoked API key");
        }
        catch (HttpRequestException ex) when (ex.StatusCode is { } status)
        {
            return new DiagnosticCheckResult(
                Name,
                DiagnosticStatus.Fail,
                $"Auth verify: FAIL — server returned {(int)status}");
        }
        catch (HttpRequestException ex)
        {
            return new DiagnosticCheckResult(
                Name,
                DiagnosticStatus.Fail,
                $"Auth verify: FAIL — could not reach the API ({ex.GetType().Name})");
        }
        catch (Exception ex)
        {
            // The SDK surfaces a malformed body as JsonException, an unexpected
            // content type as NotSupportedException, and a null body as
            // InvalidOperationException. A diagnostic check must not throw, so
            // anything unrecognised still degrades to a FAIL result.
            return new DiagnosticCheckResult(
                Name,
                DiagnosticStatus.Fail,
                $"Auth verify: FAIL — the request failed ({ex.GetType().Name})");
        }
    }
}
