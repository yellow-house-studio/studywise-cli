namespace Studywise.Cli.Commands.Doctor;

public sealed record DoctorCommandOptions(bool Json, string CheckName = "all");