using System.CommandLine;
using Studywise.Cli.Configuration;
using Studywise.Cli.Diagnostics;
using Studywise.Cli.Diagnostics.Checks;
using Studywise.Cli.Diagnostics.Formatting;
using Studywise.Cli.Formatting;
using Studywise.Cli.Http;

namespace Studywise.Cli.Commands.Doctor;

/// <summary>
/// Handler for the <c>studywise doctor</c> command. Resolves the requested check(s),
/// runs them through <see cref="IDiagnosticRunner"/>, and writes the report to the console.
/// </summary>
public sealed class DoctorCommandHandler : ICommandHandler<DoctorCommandOptions>
{
    private readonly IDiagnosticRunner _runner;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IStudywiseTransport _transport;
    private readonly ApplicationConfig _config;

    /// <summary>
    /// Initializes the handler with its dependencies.
    /// </summary>
    /// <param name="runner">Runs the resolved diagnostic checks.</param>
    /// <param name="httpClientFactory">Used by <c>connection</c> check.</param>
    /// <param name="transport">Used by the <c>auth-verify</c> check to hit the API.</param>
    /// <param name="config">Resolved CLI configuration (API base URL, key, etc.).</param>
    public DoctorCommandHandler(
        IDiagnosticRunner runner,
        IHttpClientFactory httpClientFactory,
        IStudywiseTransport transport,
        ApplicationConfig config)
    {
        _runner = runner;
        _httpClientFactory = httpClientFactory;
        _transport = transport;
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
                new AuthVerifyDiagnosticCheck(_transport, _config)
            },
            "config" => new[] { new ConfigDiagnosticCheck() },
            "api-key" => new[] { new ApiKeyDiagnosticCheck() },
            "connection" => new[] { new ConnectionDiagnosticCheck(_httpClientFactory) },
            "auth-verify" => new[] { new AuthVerifyDiagnosticCheck(_transport, _config) },
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
