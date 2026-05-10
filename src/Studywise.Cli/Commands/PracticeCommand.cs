using System.CommandLine;

namespace Studywise.Cli.Commands;

public sealed class PracticeCommand : ICommandRegistration
{
    public void Register(RootCommand rootCommand)
    {
        rootCommand.AddCommand(Create());
    }

    public static Command Create()
    {
        var command = new Command("practice", "Practice with flashcards");
        return command;
    }
}
