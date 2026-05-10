using System.CommandLine;

namespace Studywise.Cli.Commands;

[AutoRegisterCommand]
public static class AuthCommand
{
    public static Command Create()
    {
        var command = new Command("auth", "Authentication commands");
        command.AddAlias("login");

        var statusCommand = new Command("status", "Check authentication status");
        statusCommand.SetHandler(() =>
        {
            Console.WriteLine("Auth status: Not configured");
            Console.WriteLine("Set STUDYWISE_API_KEY environment variable for agent authentication.");
        });

        command.AddCommand(statusCommand);
        return command;
    }
}