using System.CommandLine;
using Studywise.Cli.Configuration;
using Studywise.Cli.Diagnostics;
using Studywise.Cli.Diagnostics.Checks;
using Studywise.Cli.Diagnostics.Formatting;
using Studywise.Cli.Formatting;
using Studywise.Cli.Http;

namespace Studywise.Cli.Commands.Doctor;

public sealed class DoctorCommandHandler : ICommandHandler<DoctorCommandOptions>
{
    private readonly IDiagnosticRunner _runner;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IStudywiseTransport _transport;
    private readonly ApplicationConfig _config;

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
