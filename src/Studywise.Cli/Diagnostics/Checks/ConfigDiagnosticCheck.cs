using System.Runtime.InteropServices;
using Studywise.Cli.Configuration;

namespace Studywise.Cli.Diagnostics.Checks;

/// <summary>
/// Checks whether the Studywise config file is present and readable.
/// NOTE: This check has a TOCTOU race window between File.Exists and the read attempt.
/// This is acceptable for CLI diagnostics where a subsequent operation would fail anyway.
/// </summary>
public sealed class ConfigDiagnosticCheck : IDiagnosticCheck
{
    public string Name => "config";

    public Task<DiagnosticCheckResult> RunAsync(CancellationToken cancellationToken = default)
    {
        var configPath = ApplicationConfig.GetConfigPath();

        if (File.Exists(configPath) || IsSymlink(configPath))
        {
            return CheckFileReadability(configPath, Name, cancellationToken);
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

    private static Task<DiagnosticCheckResult> CheckFileReadability(string configPath, string checkName, CancellationToken cancellationToken)
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
            // Check for cancellation after I/O to avoid synchronous cancellation overhead
            if (cancellationToken.IsCancellationRequested)
            {
                return Task.FromResult(new DiagnosticCheckResult(checkName, DiagnosticStatus.Fail, $"Config: FAIL — cancelled ({configPath})"));
            }
            return Task.FromResult(new DiagnosticCheckResult(checkName, DiagnosticStatus.Pass, $"Config: OK — {configPath}"));
        }
        catch (UnauthorizedAccessException)
        {
            return Task.FromResult(new DiagnosticCheckResult(checkName, DiagnosticStatus.Fail, $"Config: FAIL — unreadable (permission denied) ({configPath})"));
        }
        catch (IOException ex) when (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && ex.HResult == unchecked((int)0x80070020))
        {
            return Task.FromResult(new DiagnosticCheckResult(checkName, DiagnosticStatus.Fail, $"Config: FAIL — unreadable (file locked) ({configPath})"));
        }
        catch (OperationCanceledException)
        {
            return Task.FromResult(new DiagnosticCheckResult(checkName, DiagnosticStatus.Fail, $"Config: FAIL — cancelled ({configPath})"));
        }
        catch (IOException)
        {
            return Task.FromResult(new DiagnosticCheckResult(checkName, DiagnosticStatus.Fail, $"Config: FAIL — unreadable (I/O error) ({configPath})"));
        }
    }
}
