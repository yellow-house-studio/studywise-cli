using System.Diagnostics;
using System.Text.Json;

namespace Studywise.CLI.IntegrationTests;

public class DoctorCommandIntegrationTests
{
    [Fact]
    public void Doctor_DefaultMode_ProducesReadableText()
    {
        var result = RunCli("doctor", apiKey: "test-key");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Studywise CLI Diagnostics", result.StdOut);
        Assert.Contains("Config:", result.StdOut);
        Assert.Contains("API-nyckel:", result.StdOut);
        Assert.Contains("Connection:", result.StdOut);
    }

    [Fact]
    public void Doctor_JsonMode_ProducesJsonReport()
    {
        var result = RunCli("doctor --json", apiKey: "test-key");

        Assert.Equal(0, result.ExitCode);

        using var json = JsonDocument.Parse(result.StdOut);
        var root = json.RootElement;

        Assert.True(root.TryGetProperty("generatedAtUtc", out var generatedAtUtc));
        Assert.Equal(JsonValueKind.String, generatedAtUtc.ValueKind);
        Assert.True(DateTimeOffset.TryParse(generatedAtUtc.GetString(), out _));

        Assert.True(root.TryGetProperty("checks", out var checks));
        Assert.Equal(JsonValueKind.Array, checks.ValueKind);
        Assert.Equal(3, checks.GetArrayLength());
        Assert.Equal("config", checks[0].GetProperty("name").GetString());
        Assert.Equal("api-key", checks[1].GetProperty("name").GetString());
        Assert.Equal("connection", checks[2].GetProperty("name").GetString());
        Assert.Contains(checks[0].GetProperty("status").GetString(), new[] { "pass", "warn", "fail" });
        Assert.Contains(checks[1].GetProperty("status").GetString(), new[] { "pass", "warn", "fail" });
        Assert.Contains(checks[2].GetProperty("status").GetString(), new[] { "pass", "warn", "fail" });
        Assert.True(checks[0].TryGetProperty("message", out var configMessage));
        Assert.False(string.IsNullOrWhiteSpace(configMessage.GetString()));
        Assert.True(checks[1].TryGetProperty("message", out var apiKeyMessage));
        Assert.False(string.IsNullOrWhiteSpace(apiKeyMessage.GetString()));
        Assert.True(checks[2].TryGetProperty("message", out var connectionMessage));
        Assert.False(string.IsNullOrWhiteSpace(connectionMessage.GetString()));
        var passedCount = root.GetProperty("passedCount").GetInt32();
        var failedCount = root.GetProperty("failedCount").GetInt32();
        var warningCount = root.GetProperty("warningCount").GetInt32();

        Assert.Equal(3, passedCount + failedCount + warningCount);
        Assert.Equal(0, failedCount);
        Assert.True(root.GetProperty("isSuccess").GetBoolean());
    }

    [Fact]
    public void Doctor_ReturnsFailureWhenApiKeyIsMissing()
    {
        var result = RunCli("doctor");

        Assert.Equal(1, result.ExitCode);
        Assert.Contains("API-nyckel: FAIL", result.StdOut);
    }

    private static (int ExitCode, string StdOut, string StdErr) RunCli(string arguments, string? apiKey = null)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "/home/robert/.dotnet/dotnet",
            Arguments = $"run --project src/Studywise.Cli/Studywise.Cli.csproj -- {arguments}",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            WorkingDirectory = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../../"))
        };

        startInfo.Environment["STUDYWISE_API_BASE_URL"] = "http://127.0.0.1:65535";

        if (apiKey is null)
        {
            startInfo.Environment.Remove("STUDYWISE_API_KEY");
        }
        else
        {
            startInfo.Environment["STUDYWISE_API_KEY"] = apiKey;
        }

        using var process = Process.Start(startInfo);
        if (process is null)
        {
            throw new InvalidOperationException("Failed to start CLI process.");
        }

        var stdOut = process.StandardOutput.ReadToEnd();
        var stdErr = process.StandardError.ReadToEnd();
        process.WaitForExit();

        return (process.ExitCode, stdOut, stdErr);
    }
}
