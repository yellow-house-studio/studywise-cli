using System.CommandLine;
using Studywise.Cli.Commands;

var rootCommand = new RootCommand("Studywise CLI - Client CLI for agents and end users");

rootCommand.AddCommand(WordsCommand.Create());
rootCommand.AddCommand(ProgressCommand.Create());
rootCommand.AddCommand(PracticeCommand.Create());

return await rootCommand.InvokeAsync(args);