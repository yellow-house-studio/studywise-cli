using System.Diagnostics;
using System.Text.Json;

namespace Studywise.CLI.E2ETests;

public class DoctorCommandE2ETests
{
    [Fact]
    public void DoctorJson_OutputsMachineReadableDiagnostics()
    {
        using var devProxy = TryStartDevProxy();
        if (devProxy is null)
        {
            return;
        }

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
        var mocksFile = Path.Combine(repoRoot, "test", "Studywise.CLI.E2ETests", "doctor-mocks.json");

        foreach (var candidate in new[] { "devproxy", "/home/robert/.dotnet/tools/devproxy" })
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = candidate,
                Arguments = $"--mocks-urls \"{mocksFile}\"",
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
                    Thread.Sleep(1000);
                    return process;
                }
            }
            catch
            {
            }
        }

        return null;
    }
}
