using System.Diagnostics;
using System.Text.Json;

namespace Studywise.CLI.E2ETests;

public class DoctorCommandE2ETests
{
    [Fact]
    public void DoctorJson_OutputsMachineReadableDiagnostics()
    {
        using var devProxy = TryStartDevProxy();
        Assert.True(devProxy is not null, "Failed to start Dev Proxy; install or run Dev Proxy so doctor --json E2E coverage executes.");

        var dotnetPath = GetDotnetPath();
        var repoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));

        var startInfo = new ProcessStartInfo
        {
            FileName = dotnetPath,
            Arguments = "run --project src/Studywise.Cli/Studywise.Cli.csproj -- doctor --json",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            WorkingDirectory = repoRoot
        };

        startInfo.Environment["STUDYWISE_API_KEY"] = "test-key";
        startInfo.Environment["STUDYWISE_API_BASE_URL"] = "http://127.0.0.1:8000";

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
        var passedCount = root.GetProperty("passedCount").GetInt32();
        var failedCount = root.GetProperty("failedCount").GetInt32();
        var warningCount = root.GetProperty("warningCount").GetInt32();

        Assert.Equal(3, passedCount + failedCount + warningCount);
        Assert.Equal(0, failedCount);
        Assert.True(root.GetProperty("isSuccess").GetBoolean());
        Assert.True(string.IsNullOrWhiteSpace(stdErr), $"Unexpected stderr: {stdErr}");
    }

    private static Process? TryStartDevProxy()
    {
        var repoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));

        foreach (var candidate in new[] { "devproxy", "/usr/local/bin/devproxy", "/opt/homebrew/bin/devproxy" })
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = candidate,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                WorkingDirectory = repoRoot
            };

            try
            {
                var process = Process.Start(startInfo);
                if (process is not null && !process.HasExited)
                {
                    Thread.Sleep(2000);
                    return process;
                }
            }
            catch
            {
            }
        }

        return null;
    }

    private static string GetDotnetPath()
    {
        var dotnetExe = Environment.GetEnvironmentVariable("DOTNET_ROOT") is not null
            ? Path.Combine(Environment.GetEnvironmentVariable("DOTNET_ROOT")!, "dotnet")
            : "dotnet";

        if (OperatingSystem.IsWindows())
            dotnetExe += ".exe";

        if (File.Exists(dotnetExe))
            return dotnetExe;

        return "dotnet";
    }
}
