namespace Studywise.Cli.Errors;

/// <summary>
/// The stable set of failure kinds a command can report. The set is a contract:
/// adding a kind is non-breaking, renaming or removing one is breaking.
/// </summary>
public enum CliErrorKind
{
    /// <summary>The request was malformed or a required input was missing.</summary>
    Validation,

    /// <summary>The request conflicted with the current state of the resource.</summary>
    Conflict,

    /// <summary>The request was not authenticated.</summary>
    Unauthorized,

    /// <summary>The addressed resource does not exist.</summary>
    NotFound,

    /// <summary>The caller is authenticated but not permitted.</summary>
    Forbidden,

    /// <summary>The caller exceeded a rate limit.</summary>
    RateLimit,

    /// <summary>The API failed to handle the request.</summary>
    Server,

    /// <summary>The API could not be reached.</summary>
    Network,

    /// <summary>The request did not complete in time.</summary>
    Timeout,

    /// <summary>The command was invoked incorrectly.</summary>
    Usage
}
