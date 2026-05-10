namespace Studywise.Cli.Commands;

/// <summary>
/// Marker attribute for commands that should be auto-registered.
/// Add this attribute to a command class to have it auto-registered at startup.
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public class AutoRegisterCommandAttribute : Attribute
{
    // Marker attribute - no properties needed
}