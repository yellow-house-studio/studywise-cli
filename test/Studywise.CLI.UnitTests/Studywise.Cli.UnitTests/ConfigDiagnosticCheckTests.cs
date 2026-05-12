using Studywise.Cli.Diagnostics;
using Studywise.Cli.Diagnostics.Checks;

namespace Studywise.Cli.UnitTests;

public class ConfigDiagnosticCheckTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _originalConfigEnv;

    public ConfigDiagnosticCheckTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"studywise_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
        _originalConfigEnv = Environment.GetEnvironmentVariable("STUDYWISE_CONFIG") ?? "";
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable("STUDYWISE_CONFIG", _originalConfigEnv);
        Directory.Delete(_tempDir, recursive: true);
    }

    private void SetConfigPath(string path)
    {
        Environment.SetEnvironmentVariable("STUDYWISE_CONFIG", path);
    }

    private void ClearConfigPath()
    {
        Environment.SetEnvironmentVariable("STUDYWISE_CONFIG", "");
    }

    private static bool IsBrokenSymlink(string path)
    {
        try
        {
            var info = new FileInfo(path);
            return info.LinkTarget != null && !info.Exists;
        }
        catch
        {
            return false;
        }
    }

    [Fact]
    public async Task RunAsync_PassWhenConfigExistsAndReadable()
    {
        var configPath = Path.Combine(_tempDir, "config.json");
        File.WriteAllText(configPath, "{}");
        SetConfigPath(configPath);

        var check = new ConfigDiagnosticCheck();
        var result = await check.RunAsync();

        Assert.Equal(DiagnosticStatus.Pass, result.Status);
        Assert.Contains("OK", result.Message);
        Assert.Contains(configPath, result.Message);
    }

    [Fact]
    public async Task RunAsync_FailWhenConfigMissing()
    {
        var configPath = Path.Combine(_tempDir, "nonexistent.json");
        SetConfigPath(configPath);

        var check = new ConfigDiagnosticCheck();
        var result = await check.RunAsync();

        Assert.Equal(DiagnosticStatus.Fail, result.Status);
        Assert.Contains("missing", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(configPath, result.Message);
    }

    [Fact]
    public async Task RunAsync_FailWhenConfigUnreadable_PermissionDenied()
    {
        var configPath = Path.Combine(_tempDir, "unreadable.json");
        File.WriteAllText(configPath, "{}");
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(configPath, UnixFileMode.None);
        }
        else
        {
            var fileInfo = new FileInfo(configPath) { IsReadOnly = true };
        }
        SetConfigPath(configPath);

        var check = new ConfigDiagnosticCheck();
        var result = await check.RunAsync();

        Assert.Equal(DiagnosticStatus.Fail, result.Status);
        Assert.Contains("unreadable", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("permission", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(configPath, result.Message);
    }

    [Fact]
    public async Task RunAsync_FailWhenConfigUnreadable_FileLocked()
    {
        var configPath = Path.Combine(_tempDir, "locked.json");
        File.WriteAllText(configPath, "{}");
        SetConfigPath(configPath);

        var check = new ConfigDiagnosticCheck();
        await using (var lockedStream = new FileStream(configPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            var result = await check.RunAsync();

            Assert.Equal(DiagnosticStatus.Fail, result.Status);
            Assert.Contains("unreadable", result.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("locked", result.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Contains(configPath, result.Message);
        }
    }

    [Fact]
    public async Task RunAsync_EmptyConfigEnvTreatedAsUnset()
    {
        Environment.SetEnvironmentVariable("STUDYWISE_CONFIG", "");
        var check = new ConfigDiagnosticCheck();
        var result = await check.RunAsync();

        Assert.Equal(DiagnosticStatus.Fail, result.Status);
        Assert.Contains("missing", result.Message, StringComparison.OrdinalIgnoreCase);
    }
}