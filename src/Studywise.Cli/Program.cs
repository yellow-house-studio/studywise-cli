using System.CommandLine;
using Microsoft.Extensions.DependencyInjection;
using Studywise.Cli.Commands;
using Studywise.Cli.Configuration;
using Studywise.Cli.Diagnostics;
using Studywise.Cli.Services;

// Load configuration from environment
var config = ApplicationConfig.FromEnvironment();

// Build service collection with DI
var services = new ServiceCollection();

// Register HttpClient with IHttpClientFactory
services.AddHttpClient(StudywiseDefaults.ApiName, client =>
{
    client.BaseAddress = new Uri(config.ApiBaseUrl);
    client.DefaultRequestHeaders.Add("User-Agent", config.UserAgent);
});

// Register services
services.AddSingleton(config);
services.AddSingleton<DiagnosticRunner>();
services.AddSingleton<DoctorService>();

// Build service provider
var serviceProvider = services.BuildServiceProvider();

// Build root command
var rootCommand = new RootCommand("Studywise CLI - Client CLI for agents and end users");

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
    if (command is not null)
    {
        rootCommand.AddCommand(command);
    }
}

return await rootCommand.InvokeAsync(args);