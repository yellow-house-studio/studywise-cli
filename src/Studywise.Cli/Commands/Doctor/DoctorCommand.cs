using System.CommandLine;
using System.CommandLine.Invocation;

namespace Studywise.Cli.Commands.Doctor;

/// <summary>
/// The <c>studywise doctor</c> command. Runs diagnostic checks against the local
/// environment and the Studywise API and reports PASS / WARN / FAIL.
/// </summary>
public sealed class DoctorCommand : Command
{
    private readonly ICommandHandler<DoctorCommandOptions> _handler;

    /// <summary>
    /// Initializes the command with its options (<c>--json</c>, <c>--check</c>) and
    /// wires the handler invocation.
    /// </summary>
    /// <param name="handler">The handler invoked when the command runs.</param>
    public DoctorCommand(ICommandHandler<DoctorCommandOptions> handler)
        : base("doctor", "Run CLI diagnostics checks")
    {
        _handler = handler;

        var jsonOption = new Option<bool>("--json", "Output diagnostics as JSON");
        AddOption(jsonOption);

        var checkOption = new Option<string>(
            name: "--check",
            description: "Which check to run: config, api-key, connection, auth-verify, or all (default)",
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
