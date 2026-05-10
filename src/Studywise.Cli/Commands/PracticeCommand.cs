using System.CommandLine;

namespace Studywise.Cli.Commands;

[AutoRegisterCommand]
public static class PracticeCommand
{
    public static Command Create()
    {
        var command = new Command("practice", "Practice with flashcards");
        return command;
    }
}