namespace Studywise.Cli.Commands.Doctor;

/// <summary>
/// Parsed options for the <c>studywise doctor</c> command.
/// </summary>
/// <param name="Json">When true, emit the report as JSON instead of the default text formatter.</param>
/// <param name="CheckName">
/// Which single check to run (one of <c>config</c>, <c>api-key</c>, <c>connection</c>,
/// <c>auth-verify</c>, or <c>all</c>). Defaults to <c>all</c>.
/// </param>
public sealed record DoctorCommandOptions(bool Json, string CheckName = "all");
