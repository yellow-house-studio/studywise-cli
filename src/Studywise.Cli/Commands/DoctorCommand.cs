using System.CommandLine;
using System.CommandLine.Invocation;
using Studywise.Cli.Diagnostics;
using Studywise.Cli.Diagnostics.Checks;
using Studywise.Cli.Diagnostics.Formatting;

namespace Studywise.Cli.Commands;

public sealed class DoctorCommand : ICommandRegistration
{
    public void Register(RootCommand rootCommand)
    {
        rootCommand.AddCommand(Create());
    }

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
        var checks = new IDiagnosticCheck[]
        {
            new ConfigDiagnosticCheck(),
            new ApiKeyDiagnosticCheck(),
            new ConnectionDiagnosticCheck()
        };

        var runner = new DiagnosticRunner();
        var report = await runner.RunAsync(checks, context.GetCancellationToken());
        var asJson = context.ParseResult.GetValueForOption(jsonOption);

        var output = asJson
            ? new JsonDiagnosticReportFormatter().Format(report)
            : new TextDiagnosticReportFormatter().Format(report);

        context.Console.WriteLine(output);
        context.ExitCode = report.IsSuccess ? 0 : 1;
    }
}
