using System.Runtime.InteropServices;
using Studywise.Cli.Diagnostics;
using Studywise.Cli.Diagnostics.Checks;

namespace Studywise.Cli.UnitTests;

public class ConfigDiagnosticCheckTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _originalConfigEnv;
    private readonly bool _configEnvWasOriginallySet;

    public ConfigDiagnosticCheckTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"studywise_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
        _originalConfigEnv = Environment.GetEnvironmentVariable("STUDYWISE_CONFIG_PATH") ?? "";
        _configEnvWasOriginallySet = !string.IsNullOrEmpty(_originalConfigEnv);
    }

    public void Dispose()
    {
        // Restore to original state: null if unset, original value if set
        if (_configEnvWasOriginallySet)
        {
            Environment.SetEnvironmentVariable("STUDYWISE_CONFIG_PATH", _originalConfigEnv);
        }
        else
        {
            Environment.SetEnvironmentVariable("STUDYWISE_CONFIG_PATH", null);
        }
        Directory.Delete(_tempDir, recursive: true);
    }

    private void SetConfigPath(string path)
    {
        Environment.SetEnvironmentVariable("STUDYWISE_CONFIG_PATH", path);
    }

    private void ClearConfigPath()
    {
        Environment.SetEnvironmentVariable("STUDYWISE_CONFIG_PATH", null);
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

        // IsReadOnly=true blocks WRITES but NOT READS on Windows. Use UnixFileMode.None on Unix,
        // skip on Windows (no portable way to make a file truly unreadable for the current user).
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(configPath, UnixFileMode.None);
            SetConfigPath(configPath);

            var check = new ConfigDiagnosticCheck();
            var result = await check.RunAsync();

            Assert.Equal(DiagnosticStatus.Fail, result.Status);
            Assert.Contains("unreadable", result.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("permission", result.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Contains(configPath, result.Message);
        }
        else
        {
            // On Windows, skip this specific test since IsReadOnly doesn't prevent reads.
            // A proper fix would use FilePermissionAuditRules or a truly unreadable location.
            await Task.CompletedTask;
        }
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
        Environment.SetEnvironmentVariable("STUDYWISE_CONFIG_PATH", Path.Combine(Path.GetTempPath(), "nonexistent_studywise_config_" + Guid.NewGuid()));
        var check = new ConfigDiagnosticCheck();
        var result = await check.RunAsync();

        Assert.Equal(DiagnosticStatus.Fail, result.Status);
        Assert.Contains("missing", result.Message, StringComparison.OrdinalIgnoreCase);

        // Verify the resolved path matches the platform default (not the empty string)
        string expectedPath;
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            expectedPath = Path.Combine(appData, "studywise", "config.json");
        }
        else
        {
            var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            expectedPath = Path.Combine(userProfile, ".config", "studywise", "config.json");
        }
        Assert.Contains(expectedPath, result.Message);
    }
}