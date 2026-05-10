namespace Studywise.Cli.Commands;

public interface ICommandHandler<in TOptions>
{
    Task<int> HandleAsync(
        TOptions options,
        System.CommandLine.IConsole console,
        CancellationToken cancellationToken);
}