namespace Studywise.Cli.Diagnostics.Checks;

public sealed class ConfigDiagnosticCheck : IDiagnosticCheck
{
    public string Name => "config";

    public Task<DiagnosticCheckResult> RunAsync(CancellationToken cancellationToken = default)
    {
        var configPath = ResolveConfigPath();

        if (File.Exists(configPath) || IsSymlink(configPath))
        {
            return CheckFileReadability(configPath, Name);
        }

        return Task.FromResult(new DiagnosticCheckResult(Name, DiagnosticStatus.Fail, $"Config: FAIL — missing ({configPath})"));
    }

    private static bool IsSymlink(string path)
    {
        try
        {
            return new FileInfo(path).LinkTarget != null;
        }
        catch
        {
            return false;
        }
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

    private static string ResolveConfigPath()
    {
        var envOverride = Environment.GetEnvironmentVariable("STUDYWISE_CONFIG");
        if (!string.IsNullOrEmpty(envOverride))
        {
            return envOverride;
        }

        return GetPlatformDefaultPath();
    }

    private static string GetPlatformDefaultPath()
    {
        if (OperatingSystem.IsWindows())
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            return Path.Combine(appData, "studywise", "config.json");
        }

        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return Path.Combine(userProfile, ".config", "studywise", "config.json");
    }

    private static Task<DiagnosticCheckResult> CheckFileReadability(string configPath, string checkName)
    {
        // Check for broken symlink before attempting to open (avoids spurious I/O error on broken symlink)
        if (IsBrokenSymlink(configPath))
        {
            return Task.FromResult(new DiagnosticCheckResult(checkName, DiagnosticStatus.Fail, $"Config: FAIL — unreadable (broken symlink) ({configPath})"));
        }

        try
        {
            using var stream = new FileStream(configPath, FileMode.Open, FileAccess.Read, FileShare.Read);
            stream.ReadByte();
            return Task.FromResult(new DiagnosticCheckResult(checkName, DiagnosticStatus.Pass, $"Config: OK — {configPath}"));
        }
        catch (UnauthorizedAccessException)
        {
            return Task.FromResult(new DiagnosticCheckResult(checkName, DiagnosticStatus.Fail, $"Config: FAIL — unreadable (permission denied) ({configPath})"));
        }
        catch (IOException ex) when (ex.HResult == unchecked((int)0x80070020))
        {
            return Task.FromResult(new DiagnosticCheckResult(checkName, DiagnosticStatus.Fail, $"Config: FAIL — unreadable (file locked) ({configPath})"));
        }
        catch (IOException)
        {
            return Task.FromResult(new DiagnosticCheckResult(checkName, DiagnosticStatus.Fail, $"Config: FAIL — unreadable (I/O error) ({configPath})"));
        }
    }
}