using System.CommandLine;
using System.CommandLine.Invocation;

namespace Studywise.Cli.Commands.Doctor;

public sealed class DoctorCommand : Command
{
    private readonly ICommandHandler<DoctorCommandOptions> _handler;

    public DoctorCommand(ICommandHandler<DoctorCommandOptions> handler)
        : base("doctor", "Run CLI diagnostics checks")
    {
        _handler = handler;

        var jsonOption = new Option<bool>("--json", "Output diagnostics as JSON");
        AddOption(jsonOption);

        var checkOption = new Option<string>(
            name: "--check",
            description: "Which check to run: config, api-key, connection, or all (default)",
            getDefaultValue: () => "all");
        AddOption(checkOption);

        System.CommandLine.Handler.SetHandler(
            this,
            async (InvocationContext context) =>
            {
                var options = new DoctorCommandOptions(
                    Json: context.ParseResult.GetValueForOption(jsonOption),
                    CheckName: context.ParseResult.GetValueForOption(checkOption) ?? "all");

                context.ExitCode = await _handler.HandleAsync(
                    options,
                    context.Console,
                    context.GetCancellationToken());
            });
    }
}