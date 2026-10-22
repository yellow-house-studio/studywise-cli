using System.CommandLine;
using System.Net;
using Studywise.Cli.Configuration;
using Studywise.Cli.Errors;
using Studywise.Cli.Formatting;
using Studywise.Contracts.Http.Auth;

namespace Studywise.Cli.Commands.Auth;

/// <summary>
/// Handler for the <c>studywise auth verify</c> command. Verifies the configured API key
/// against the API through the SDK and reports which user the key authenticates as.
/// </summary>
/// <param name="authClient">SDK client used to verify the credentials.</param>
/// <param name="config">Resolved CLI configuration (the API key and base URL come from here).</param>
public sealed class AuthVerifyCommandHandler(AuthClient authClient, ApplicationConfig config)
    : ICommandHandler<AuthVerifyCommandOptions>
{
    private const string CommandPrefix = "studywise auth verify:";

    /// <summary>The HTTP method the SDK uses for the auth verification request.</summary>
    private const string RequestMethod = "GET";

    /// <summary>The path of the API endpoint that validates the API key.</summary>
    private const string RequestPath = "/api/v1/auth/verify";

    /// <summary>
    /// Mirrors the SDK's default <c>StudywiseClientOptions.Timeout</c>, so the message
    /// reports the bound the request actually ran under.
    /// </summary>
    private const int RequestTimeoutSeconds = 30;

    /// <summary>
    /// Executes the command.
    /// </summary>
    /// <param name="options">The parsed command options.</param>
    /// <param name="console">Console for standard and error output.</param>
    /// <param name="cancellationToken">Token observed for cancellation.</param>
    /// <returns>0 on success, 2 for a usage error, 1 for an operational failure.</returns>
    public async Task<int> HandleAsync(
        AuthVerifyCommandOptions options,
        IConsole console,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(config.ApiKey))
        {
            return Fail(
                console,
                options,
                CliErrorKind.Usage,
                exitCode: 2,
                cause: "no API key is configured",
                hint: "set STUDYWISE_API_KEY",
                details: null);
        }

        try
        {
            var verification = await authClient.VerifyAsync(cancellationToken);

            var result = new AuthVerifyResult(
                UserId: string.IsNullOrEmpty(verification.UserId) ? "unknown" : verification.UserId,
                AuthMethod: string.IsNullOrEmpty(verification.AuthMethod) ? "unknown" : verification.AuthMethod);

            if (options.Json)
            {
                console.WriteLine(JsonReporter.Format(result));
            }
            else
            {
                console.WriteLine($"Auth verify: OK — connected as {result.UserId} ({result.AuthMethod})");
            }

            return 0;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Caller-cancelled - rethrow so callers can observe it.
            throw;
        }
        catch (OperationCanceledException)
        {
            return Fail(
                console,
                options,
                CliErrorKind.Timeout,
                exitCode: 1,
                cause: $"timed out after {RequestTimeoutSeconds}s waiting on {config.ApiBaseUrl}",
                hint: "try again",
                details: null);
        }
        catch (HttpRequestException ex) when (ex.StatusCode is { } status)
        {
            return FailHttp(console, options, status);
        }
        catch (HttpRequestException ex)
        {
            return Fail(
                console,
                options,
                CliErrorKind.Network,
                exitCode: 1,
                cause: $"connection to {config.ApiBaseUrl} failed: {ex.Message}",
                hint: "check the network and the API base URL",
                details: null);
        }
    }

    private static int FailHttp(IConsole console, AuthVerifyCommandOptions options, HttpStatusCode status)
    {
        var (kind, cause, hint) = status switch
        {
            HttpStatusCode.Unauthorized => (
                CliErrorKind.Unauthorized,
                "HTTP 401 Unauthorized: the API key was rejected",
                "check the key and try again"),
            HttpStatusCode.Forbidden => (
                CliErrorKind.Forbidden,
                "HTTP 403 Forbidden: the API key may not perform this request",
                "check the key's permissions"),
            HttpStatusCode.NotFound => (
                CliErrorKind.NotFound,
                "HTTP 404 Not Found: the auth verify endpoint was not found",
                "check the API base URL"),
            HttpStatusCode.TooManyRequests => (
                CliErrorKind.RateLimit,
                "HTTP 429 Too Many Requests: the API is rate limiting this key",
                "slow down and try again"),
            _ when (int)status >= 500 => (
                CliErrorKind.Server,
                $"HTTP {(int)status} {status}: the API returned an error",
                "try again later"),
            _ => (
                CliErrorKind.Validation,
                $"HTTP {(int)status} {status}: the API rejected the request",
                "check the command's arguments")
        };

        return Fail(console, options, kind, exitCode: 1, cause, hint, new { statusCode = (int)status });
    }

    private static int Fail(
        IConsole console,
        AuthVerifyCommandOptions options,
        CliErrorKind kind,
        int exitCode,
        string cause,
        string hint,
        object? details)
    {
        var message = $"{cause} ({hint})";

        WriteError(console, $"{CommandPrefix} {message}");

        if (options.Json)
        {
            var envelope = new CliErrorEnvelope(
                exitCode,
                new CliErrorBody(kind, message, new CliErrorRequest(RequestMethod, RequestPath), details));

            WriteError(console, JsonReporter.Format(envelope));
        }

        return exitCode;
    }

    /// <summary>
    /// Writes a line to the error stream. <c>IStandardStreamWriter</c> has no
    /// <c>WriteLine</c>, so the newline is written explicitly.
    /// </summary>
    /// <param name="console">Console to write to.</param>
    /// <param name="text">The text to write.</param>
    private static void WriteError(IConsole console, string text)
    {
        console.Error.Write(text);
        console.Error.Write(Environment.NewLine);
    }
}
