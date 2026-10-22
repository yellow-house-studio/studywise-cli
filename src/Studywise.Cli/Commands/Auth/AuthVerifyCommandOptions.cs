namespace Studywise.Cli.Commands.Auth;

/// <summary>
/// Parsed options for the <c>studywise auth verify</c> command.
/// </summary>
/// <param name="Json">When true, emit the result as JSON instead of the default human line.</param>
public sealed record AuthVerifyCommandOptions(bool Json);
