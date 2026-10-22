using System.CommandLine;
using System.CommandLine.Invocation;

namespace Studywise.Cli.Commands.Auth;

/// <summary>
/// The <c>studywise auth verify</c> command. Confirms that the configured API key
/// authenticates and reports which user it authenticates as.
/// </summary>
public sealed class AuthVerifyCommand : Command
{
    private readonly ICommandHandler<AuthVerifyCommandOptions> _handler;

    /// <summary>
    /// Initializes the command with its options (<c>--json</c>) and wires the handler invocation.
    /// </summary>
    /// <param name="handler">The handler invoked when the command runs.</param>
    public AuthVerifyCommand(ICommandHandler<AuthVerifyCommandOptions> handler)
        : base("verify", "Verify that the configured API key authenticates")
    {
        _handler = handler;

        var jsonOption = new Option<bool>("--json", "Output the result as JSON");
        AddOption(jsonOption);

        System.CommandLine.Handler.SetHandler(
            this,
            async (InvocationContext context) =>
            {
                var options = new AuthVerifyCommandOptions(
                    Json: context.ParseResult.GetValueForOption(jsonOption));

                context.ExitCode = await _handler.HandleAsync(
                    options,
                    context.Console,
                    context.GetCancellationToken());
            });
    }
}
