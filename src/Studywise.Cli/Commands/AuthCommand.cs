using System.CommandLine;
using Microsoft.Extensions.DependencyInjection;

namespace Studywise.Cli.Commands;

[AutoRegisterCommand]
public sealed class AuthCommand
{
    public static Command Create()
    {
        var command = new Command("auth", "Authentication commands");
        command.AddAlias("login");

        var statusCommand = new Command("status", "Check authentication status");
        statusCommand.SetHandler(async context =>
        {
            var httpClientFactory = context.BindingContext
                .GetRequiredService<IHttpClientFactory>();
            var httpClient = httpClientFactory.CreateClient("Studywise");

            Console.WriteLine("Auth status: Not configured");
            Console.WriteLine("Set STUDYWISE_API_KEY environment variable for agent authentication.");
            Console.WriteLine($"HTTP client configured: {httpClient?.BaseAddress is not null}");
        });

        command.AddCommand(statusCommand);
        return command;
    }
}
