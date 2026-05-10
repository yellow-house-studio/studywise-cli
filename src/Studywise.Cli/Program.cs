using System.CommandLine;
using System.Reflection;
using Studywise.Cli.Commands;

// Build root command
var rootCommand = new RootCommand("Studywise CLI - Client CLI for agents and end users");

var registrations = Assembly.GetExecutingAssembly()
    .GetTypes()
    .Where(type => typeof(ICommandRegistration).IsAssignableFrom(type)
        && type is { IsClass: true, IsAbstract: false })
    .OrderBy(type => type.Name)
    .Select(type => (ICommandRegistration)Activator.CreateInstance(type)!)
    .ToList();

foreach (var registration in registrations)
{
    registration.Register(rootCommand);
}

return await rootCommand.InvokeAsync(args);
