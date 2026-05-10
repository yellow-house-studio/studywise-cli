using System.CommandLine;
using Studywise.Cli.Commands;

// Build root command
var rootCommand = new RootCommand("Studywise CLI - Client CLI for agents and end users");

rootCommand.AddCommand(AuthCommand.Create());
rootCommand.AddCommand(WordsCommand.Create());
rootCommand.AddCommand(ProgressCommand.Create());
rootCommand.AddCommand(PracticeCommand.Create());
rootCommand.AddCommand(DoctorCommand.Create());

return await rootCommand.InvokeAsync(args);
