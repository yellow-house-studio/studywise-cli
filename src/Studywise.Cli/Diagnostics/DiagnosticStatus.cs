namespace Studywise.Cli.Diagnostics;

/// <summary>Outcome of a single diagnostic check.</summary>
public enum DiagnosticStatus
{
    /// <summary>Check succeeded.</summary>
    Pass,

    /// <summary>Check failed; report's <c>IsSuccess</c> becomes false and the doctor exit code becomes 1.</summary>
    Fail,

    /// <summary>Check could not run (e.g. precondition not met) but this is not a failure.</summary>
    Warn,
}
