using System.CommandLine;
using System.CommandLine.Invocation;
using System.Net.Http;
using Studywise.Cli.Configuration;
using Studywise.Cli.Diagnostics;
using Studywise.Cli.Diagnostics.Checks;
using Studywise.Cli.Diagnostics.Formatting;
using Studywise.Cli.Formatting;

namespace Studywise.Cli.Commands;

[AutoRegisterCommand]
public sealed class DoctorCommand
{
    public static Command Create()
    {
        var command = new Command("doctor", "Run CLI diagnostics checks");
        var jsonOption = new Option<bool>("--json", "Output diagnostics as JSON");
        command.AddOption(jsonOption);

        command.SetHandler(async context => await RunAsync(context, jsonOption));
        return command;
    }

    private static async Task RunAsync(InvocationContext context, Option<bool> jsonOption)
    {
        var config = ApplicationConfig.FromEnvironment();
        var httpClient = new HttpClient { BaseAddress = new Uri(config.ApiBaseUrl) };

        var checks = new IDiagnosticCheck[]
        {
            new ConfigDiagnosticCheck(),
            new ApiKeyDiagnosticCheck(config),
            new ConnectionDiagnosticCheck(httpClient)
        };

        var runner = new DiagnosticRunner();
        var report = await runner.RunAsync(checks, context.GetCancellationToken());
        var asJson = context.ParseResult.GetValueForOption(jsonOption);

        var output = asJson
            ? JsonReporter.Format(report)
            : new TextDiagnosticReportFormatter().Format(report);

        context.Console.WriteLine(output);
        context.ExitCode = report.IsSuccess ? 0 : 1;
    }
}
