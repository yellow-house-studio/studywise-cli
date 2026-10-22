using System.CommandLine;

namespace Studywise.Cli.Commands.Auth;

/// <summary>
/// The <c>studywise auth</c> noun. Groups the authentication commands.
/// </summary>
public sealed class AuthCommand : Command
{
    /// <summary>
    /// Initializes the noun with its subcommands.
    /// </summary>
    /// <param name="handler">The handler invoked by the <c>verify</c> subcommand.</param>
    public AuthCommand(ICommandHandler<AuthVerifyCommandOptions> handler)
        : base("auth", "Authentication commands")
    {
        AddCommand(new AuthVerifyCommand(handler));
    }
}
