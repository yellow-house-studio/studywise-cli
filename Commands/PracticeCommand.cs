using System.CommandLine;

namespace Studywise.Cli.Commands;

public static class PracticeCommand
{
    public static Command Create()
    {
        var command = new System.CommandLine.Command("practice", "Start or manage practice sessions");
        return command;
    }
}