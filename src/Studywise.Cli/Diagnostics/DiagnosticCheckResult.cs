namespace Studywise.Cli.Diagnostics;

/// <summary>Result of running a single diagnostic check.</summary>
/// <param name="Name">The check's identifier (matches <see cref="IDiagnosticCheck.Name"/>).</param>
/// <param name="Status">Whether the check passed, failed, or warned.</param>
/// <param name="Message">Human-readable English message describing the outcome.</param>
public sealed record DiagnosticCheckResult(string Name, DiagnosticStatus Status, string Message);
