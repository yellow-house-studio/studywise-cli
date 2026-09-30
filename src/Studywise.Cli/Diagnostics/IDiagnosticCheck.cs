namespace Studywise.Cli.Diagnostics;

/// <summary>
/// A single diagnostic check the doctor command can run.
/// </summary>
public interface IDiagnosticCheck
{
    /// <summary>Short identifier surfaced in doctor output (e.g. <c>"config"</c>, <c>"api-key"</c>).</summary>
    string Name { get; }

    /// <summary>
    /// Executes the check.
    /// </summary>
    /// <param name="cancellationToken">Token observed for cancellation.</param>
    /// <returns>The check's outcome.</returns>
    Task<DiagnosticCheckResult> RunAsync(CancellationToken cancellationToken = default);
}
