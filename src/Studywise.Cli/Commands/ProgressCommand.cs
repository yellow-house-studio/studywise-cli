using System.CommandLine;

namespace Studywise.Cli.Commands;

[AutoRegisterCommand]
public static class ProgressCommand
{
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