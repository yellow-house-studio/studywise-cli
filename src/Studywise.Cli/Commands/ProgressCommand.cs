using System.CommandLine;

namespace Studywise.Cli.Commands;

public sealed class ProgressCommand : ICommandRegistration
{
    public void Register(RootCommand rootCommand)
    {
        rootCommand.AddCommand(Create());
    }

    public static Command Create()
    {
        var command = new Command("progress", "Show learning progress");
        command.SetHandler(() =>
        {
            Console.WriteLine("Progress command placeholder.");
        });
        return command;
    }
}
