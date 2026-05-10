using System.CommandLine;

namespace Studywise.Cli.Commands;

public interface ICommandRegistration
{
    void Register(RootCommand rootCommand);
}
