using System.CommandLine;
using Microsoft.Extensions.DependencyInjection;
using Studywise.Cli.Commands;
using Studywise.Cli.Diagnostics;

// Build service collection with DI
var services = new ServiceCollection();

// Register HttpClient with IHttpClientFactory
services.AddHttpClient("Studywise", client =>
{
    var baseUrl = Environment.GetEnvironmentVariable("STUDYWISE_API_BASE_URL") 
                  ?? "https://api.studywise.io";
    client.BaseAddress = new Uri(baseUrl);
    client.DefaultRequestHeaders.Add("User-Agent", "Studywise-CLI/1.0");
});

// Register services
services.AddSingleton<DiagnosticRunner>();

// Build service provider
var serviceProvider = services.BuildServiceProvider();

// Build root command
var rootCommand = new RootCommand("Studywise CLI - Client CLI for agent1s and end users");

// Auto-register all commands with [AutoRegisterCommand] attribute
var assembly = typeof(Program).Assembly;
var commandTypes = assembly.GetTypes()
    .Where(t => t.IsClass 
                && !t.IsAbstract 
                && t.GetCustomAttributes(typeof(AutoRegisterCommandAttribute), false).Length > 0
                && t.GetMethod("Create") != null);

foreach (var type in commandTypes)
{
    var createMethod = type.GetMethod("Create");
    var command = createMethod?.Invoke(null, null) as Command;
    if (command != null)
    {
        rootCommand.AddCommand(command);
    }
}

return await rootCommand.InvokeAsync(args);