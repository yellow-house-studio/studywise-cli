namespace Studywise.Cli.Diagnostics;

public sealed record DiagnosticCheckResult(string Name, DiagnosticStatus Status, string Message);
