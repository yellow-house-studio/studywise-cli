using System.CommandLine;

namespace Studywise.Cli.Commands;

public static class ProgressCommand
{
    public static Command Create()
    {
        var command = new System.CommandLine.Command("progress", "View practice progress");
        return command;
    }
}