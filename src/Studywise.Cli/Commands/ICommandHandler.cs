namespace Studywise.Cli.Commands;

/// <summary>
/// Handles a parsed command by producing an exit code and writing output to the console.
/// </summary>
/// <typeparam name="TOptions">The parsed options type the handler operates on.</typeparam>
public interface ICommandHandler<in TOptions>
{
    /// <summary>
    /// Executes the command.
    /// </summary>
    /// <param name="options">The parsed command options.</param>
    /// <param name="console">Console for standard and error output.</param>
    /// <param name="cancellationToken">Token observed for cancellation.</param>
    /// <returns>The process exit code (0 for success).</returns>
    Task<int> HandleAsync(
        TOptions options,
        System.CommandLine.IConsole console,
        CancellationToken cancellationToken);
}
