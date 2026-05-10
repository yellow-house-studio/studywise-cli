using System.CommandLine;
using Studywise.Cli.Configuration;
using Studywise.Cli.Diagnostics;
using Studywise.Cli.Diagnostics.Checks;
using Studywise.Cli.Diagnostics.Formatting;
using Studywise.Cli.Formatting;

namespace Studywise.Cli.Commands;

public sealed class DoctorCommandHandler : ICommandHandler<DoctorCommandOptions>
{
    private readonly IDiagnosticRunner _runner;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ApplicationConfig _config;

    public DoctorCommandHandler(
        IDiagnosticRunner runner,
        IHttpClientFactory httpClientFactory,
        ApplicationConfig config)
    {
        _runner = runner;
        _httpClientFactory = httpClientFactory;
        _config = config;
    }

    public async Task<int> HandleAsync(
        DoctorCommandOptions options,
        IConsole console,
        CancellationToken cancellationToken)
    {
        var checks = new IDiagnosticCheck[]
        {
            new ConfigDiagnosticCheck(),
            new ApiKeyDiagnosticCheck(_config),
            new ConnectionDiagnosticCheck(_httpClientFactory)
        };

        var report = await _runner.RunAsync(checks, cancellationToken);

        var output = options.Json
            ? JsonReporter.Format(report)
            : new TextDiagnosticReportFormatter().Format(report);

        console.WriteLine(output);
        return report.IsSuccess ? 0 : 1;
    }
}