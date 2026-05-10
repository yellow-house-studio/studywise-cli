using System.CommandLine;
using System.CommandLine.Invocation;
using Microsoft.Extensions.DependencyInjection;
using Studywise.Cli.Diagnostics.Formatting;
using Studywise.Cli.Formatting;
using Studywise.Cli.Services;

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
        var service = context.BindingContext.GetRequiredService<DoctorService>();
        var report = await service.RunDiagnosticsAsync(context.GetCancellationToken());
        var asJson = context.ParseResult.GetValueForOption(jsonOption);

        var output = asJson
            ? JsonReporter.Format(report)
            : new TextDiagnosticReportFormatter().Format(report);

        context.Console.WriteLine(output);
        context.ExitCode = report.IsSuccess ? 0 : 1;
    }
}