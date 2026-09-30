namespace Studywise.Cli.Commands;

/// <summary>
/// Marker attribute for commands that should be auto-registered.
/// Apply to a command class to have it auto-registered at startup.
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public class AutoRegisterCommandAttribute : Attribute
{
}
