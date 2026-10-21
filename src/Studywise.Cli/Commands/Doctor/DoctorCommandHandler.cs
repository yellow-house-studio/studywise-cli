using System.CommandLine;
using Studywise.Cli.Configuration;
using Studywise.Cli.Diagnostics;
using Studywise.Cli.Diagnostics.Checks;
using Studywise.Cli.Diagnostics.Formatting;
using Studywise.Cli.Formatting;
using Studywise.Contracts.Http.Auth;

namespace Studywise.Cli.Commands.Doctor;

/// <summary>
/// Handler for the <c>studywise doctor</c> command. Resolves the requested check(s),
/// runs them through <see cref="IDiagnosticRunner"/>, and writes the report to the console.
/// </summary>
public sealed class DoctorCommandHandler : ICommandHandler<DoctorCommandOptions>
{
    private readonly IDiagnosticRunner _runner;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly AuthClient _authClient;
    private readonly ApplicationConfig _config;

    /// <summary>
    /// Initializes the handler with its dependencies.
    /// </summary>
    /// <param name="runner">Runs the resolved diagnostic checks.</param>
    /// <param name="httpClientFactory">Used by <c>connection</c> check.</param>
    /// <param name="authClient">SDK client used by the <c>auth-verify</c> check.</param>
    /// <param name="config">Resolved CLI configuration (API base URL, key, etc.).</param>
    public DoctorCommandHandler(
        IDiagnosticRunner runner,
        IHttpClientFactory httpClientFactory,
        AuthClient authClient,
        ApplicationConfig config)
    {
        _runner = runner;
        _httpClientFactory = httpClientFactory;
        _authClient = authClient;
        _config = config;
    }

    private IDiagnosticCheck[]? ResolveChecks(string checkName)
    {
        return checkName.ToLowerInvariant() switch
        {
            "all" => new IDiagnosticCheck[]
            {
                new ConfigDiagnosticCheck(),
                new ApiKeyDiagnosticCheck(),
                new ConnectionDiagnosticCheck(_httpClientFactory),
                new AuthVerifyDiagnosticCheck(_authClient, _config)
            },
            "config" => new[] { new ConfigDiagnosticCheck() },
            "api-key" => new[] { new ApiKeyDiagnosticCheck() },
            "connection" => new[] { new ConnectionDiagnosticCheck(_httpClientFactory) },
            "auth-verify" => new[] { new AuthVerifyDiagnosticCheck(_authClient, _config) },
            _ => null
        };
    }

    /// <summary>
    /// Executes the doctor command.
    /// </summary>
    /// <param name="options">The parsed command options.</param>
    /// <param name="console">Console for standard and error output.</param>
    /// <param name="cancellationToken">Token observed for cancellation.</param>
    /// <returns>
    /// Exit code 0 when every check passed or warned; 1 when any check failed or
    /// when the requested check name is unknown.
    /// </returns>
    public async Task<int> HandleAsync(
        DoctorCommandOptions options,
        IConsole console,
        CancellationToken cancellationToken)
    {
        IDiagnosticCheck[]? checks = ResolveChecks(options.CheckName);
        if (checks is null)
        {
            var err = console.Error;
            err.Write($"unknown check: {options.CheckName}");
            err.Write(Environment.NewLine);
            return 1;
        }

        var report = await _runner.RunAsync(checks, cancellationToken);

        var output = options.Json
            ? JsonReporter.Format(report)
            : new TextDiagnosticReportFormatter().Format(report);

        console.WriteLine(output);
        return report.IsSuccess ? 0 : 1;
    }
}
