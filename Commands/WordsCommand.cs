using System.CommandLine;

namespace Studywise.Cli.Commands;

public static class WordsCommand
{
    public static Command Create()
    {
        var command = new System.CommandLine.Command("words", "Manage word lists");
        return command;
    }
}