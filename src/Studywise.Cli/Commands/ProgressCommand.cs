using System.CommandLine;

namespace Studywise.Cli.Commands;

public static class ProgressCommand
{
    public static Command Create()
    {
        var command = new Command("progress", "View study progress");
        return command;
    }
}
