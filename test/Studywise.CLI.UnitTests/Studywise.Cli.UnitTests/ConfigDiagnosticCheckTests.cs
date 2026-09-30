using System.Runtime.InteropServices;
using Studywise.Cli.Diagnostics;
using Studywise.Cli.Diagnostics.Checks;

namespace Studywise.CLI.UnitTests;

[Category("Unit")]
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

    [Test]
    public async Task RunAsync_PassWhenConfigExistsAndReadable()
    {
        var configPath = Path.Combine(_tempDir, "config.json");
        File.WriteAllText(configPath, "{}");
        SetConfigPath(configPath);

        var check = new ConfigDiagnosticCheck();
        var result = await check.RunAsync();

        result.Status.Should().Be(DiagnosticStatus.Pass);
        result.Message.Should().Contain("OK");
        result.Message.Should().Contain(configPath);
    }

    [Test]
    public async Task RunAsync_FailWhenConfigMissing()
    {
        var configPath = Path.Combine(_tempDir, "nonexistent.json");
        SetConfigPath(configPath);

        var check = new ConfigDiagnosticCheck();
        var result = await check.RunAsync();

        result.Status.Should().Be(DiagnosticStatus.Fail);
        result.Message.Should().Contain("missing");
        result.Message.Should().Contain(configPath);
    }

    [Test]
    public async Task RunAsync_FailWhenConfigUnreadable_PermissionDenied()
    {
        var configPath = Path.Combine(_tempDir, "unreadable.json");
        File.WriteAllText(configPath, "{}");

        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(configPath, UnixFileMode.None);
            SetConfigPath(configPath);

            var check = new ConfigDiagnosticCheck();
            var result = await check.RunAsync();

            result.Status.Should().Be(DiagnosticStatus.Fail);
            result.Message.Should().Contain("unreadable");
            result.Message.Should().Contain("permission");
            result.Message.Should().Contain(configPath);
        }
        else
        {
            await Task.CompletedTask;
        }
    }

    [Test]
    public async Task RunAsync_FailWhenConfigUnreadable_FileLocked()
    {
        var configPath = Path.Combine(_tempDir, "locked.json");
        File.WriteAllText(configPath, "{}");
        SetConfigPath(configPath);

        var check = new ConfigDiagnosticCheck();
        await using (var lockedStream = new FileStream(configPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            var result = await check.RunAsync();

            result.Status.Should().Be(DiagnosticStatus.Fail);
            result.Message.Should().Contain("unreadable");
            result.Message.Should().Contain("locked");
            result.Message.Should().Contain(configPath);
        }
    }

    [Test]
    public async Task RunAsync_NonexistentConfigPathFromEnv_FailsWithPathInMessage()
    {
        var configPath = Path.Combine(Path.GetTempPath(), "nonexistent_studywise_config_" + Guid.NewGuid());
        Environment.SetEnvironmentVariable("STUDYWISE_CONFIG_PATH", configPath);
        var check = new ConfigDiagnosticCheck();
        var result = await check.RunAsync();

        result.Status.Should().Be(DiagnosticStatus.Fail);
        result.Message.Should().Contain("missing");

        Assert.That(result.Message, Does.Contain(configPath));
    }
}
