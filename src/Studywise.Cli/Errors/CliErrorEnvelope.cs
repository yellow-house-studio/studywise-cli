namespace Studywise.Cli.Errors;

/// <summary>
/// The request that failed, as reported inside the structured error envelope.
/// </summary>
/// <param name="Method">The HTTP method of the failed request.</param>
/// <param name="Path">The path of the failed request.</param>
public sealed record CliErrorRequest(string Method, string Path);

/// <summary>
/// The body of the structured error envelope.
/// </summary>
/// <param name="Kind">The stable failure kind.</param>
/// <param name="Message">The failure text, identical to the human line minus its command prefix.</param>
/// <param name="Request">The request that failed.</param>
/// <param name="Details">Command-specific extra detail. Its shape is documented per command.</param>
public sealed record CliErrorBody(
    CliErrorKind Kind,
    string Message,
    CliErrorRequest Request,
    object? Details = null);

/// <summary>
/// The structured error envelope a command emits on stderr when it fails and
/// <c>--json</c> was passed.
/// </summary>
/// <param name="ExitCode">The process exit code the command returned.</param>
/// <param name="Error">The error body.</param>
public sealed record CliErrorEnvelope(int ExitCode, CliErrorBody Error);
