using System.CommandLine;

namespace Studywise.Cli.Commands;

[AutoRegisterCommand]
public static class WordsCommand
{
    public static Command Create()
    {
        var command = new Command("words", "Manage word lists");
        
        var listCommand = new Command("list", "List all word lists");
        listCommand.SetHandler(() =>
        {
            Console.WriteLine("Word lists will be implemented in a future issue.");
        });
        
        command.AddCommand(listCommand);
        return command;
    }
}