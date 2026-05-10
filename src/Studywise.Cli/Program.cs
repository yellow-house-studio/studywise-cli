using System.CommandLine;
using Studywise.Cli.Commands;

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
    if (command != null)
    {
        rootCommand.AddCommand(command);
    }
}

return await rootCommand.InvokeAsync(args);