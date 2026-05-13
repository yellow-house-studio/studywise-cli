using System.CommandLine;
using Studywise.Cli.Configuration;
using Studywise.Cli.Diagnostics;
using Studywise.Cli.Diagnostics.Checks;
using Studywise.Cli.Diagnostics.Formatting;
using Studywise.Cli.Formatting;

namespace Studywise.Cli.Commands.Doctor;

public sealed class DoctorCommandHandler : ICommandHandler<DoctorCommandOptions>
{
    private readonly IDiagnosticRunner _runner;
    private readonly IHttpClientFactory _httpClientFactory;

    public DoctorCommandHandler(
        IDiagnosticRunner runner,
        IHttpClientFactory httpClientFactory,
        ApplicationConfig config)
    {
        _runner = runner;
        _httpClientFactory = httpClientFactory;
        _ = config;
    }

    private IDiagnosticCheck[]? ResolveChecks(string checkName)
    {
        return checkName.ToLowerInvariant() switch
        {
            "all" => new IDiagnosticCheck[]
            {
                new ConfigDiagnosticCheck(),
                new ApiKeyDiagnosticCheck(),
                new ConnectionDiagnosticCheck(_httpClientFactory)
            },
            "config" => new[] { new ConfigDiagnosticCheck() },
            "api-key" => new[] { new ApiKeyDiagnosticCheck() },
            "connection" => new[] { new ConnectionDiagnosticCheck(_httpClientFactory) },
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
