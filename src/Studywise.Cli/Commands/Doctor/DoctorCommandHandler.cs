using System.CommandLine;
using Studywise.Cli.Auth;
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
    private readonly ITokenProvider _tokenProvider;

    public DoctorCommandHandler(
        IDiagnosticRunner runner,
        IHttpClientFactory httpClientFactory,
        ITokenProvider tokenProvider)
    {
        _runner = runner;
        _httpClientFactory = httpClientFactory;
        _tokenProvider = tokenProvider;
    }

    public async Task<int> HandleAsync(
        DoctorCommandOptions options,
        IConsole console,
        CancellationToken cancellationToken)
    {
        var checks = new IDiagnosticCheck[]
        {
            new ConfigDiagnosticCheck(),
            new ApiKeyDiagnosticCheck(_tokenProvider),
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
