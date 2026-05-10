using System.Diagnostics;
using System.Text.Json;

namespace Studywise.CLI.E2ETests;

public class DoctorCommandE2ETests
{
    [Fact]
    public void DoctorJson_OutputsMachineReadableDiagnostics()
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "/home/robert/.dotnet/dotnet",
            Arguments = "run --project src/Studywise.Cli/Studywise.Cli.csproj -- doctor --json",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            WorkingDirectory = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"))
        };

        startInfo.Environment["STUDYWISE_API_KEY"] = "test-key";
        startInfo.Environment["STUDYWISE_API_BASE_URL"] = "http://127.0.0.1:65535";

        using var process = Process.Start(startInfo);
        Assert.NotNull(process);

        var stdOut = process!.StandardOutput.ReadToEnd();
        var stdErr = process.StandardError.ReadToEnd();
        process.WaitForExit();

        Assert.Equal(0, process.ExitCode);
        using var json = JsonDocument.Parse(stdOut);
        var root = json.RootElement;
        Assert.True(root.TryGetProperty("checks", out var checks));
        Assert.Equal(3, checks.GetArrayLength());
        Assert.Equal("config", checks[0].GetProperty("name").GetString());
        Assert.Equal("api-key", checks[1].GetProperty("name").GetString());
        Assert.Equal("connection", checks[2].GetProperty("name").GetString());
        Assert.True(string.IsNullOrWhiteSpace(stdErr), $"Unexpected stderr: {stdErr}");
    }
}
