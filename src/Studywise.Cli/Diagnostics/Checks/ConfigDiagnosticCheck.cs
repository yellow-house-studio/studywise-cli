using System.Runtime.InteropServices;
using Studywise.Cli.Configuration;

namespace Studywise.Cli.Diagnostics.Checks;

/// <summary>
/// Diagnostic check that reports whether the Studywise config file exists
/// and is readable by the current user.
/// <para>
/// NOTE: This check has a TOCTOU race window between <see cref="File.Exists(string)"/> and
/// the read attempt. This is acceptable for CLI diagnostics where a subsequent operation
/// would surface the same I/O error anyway.
/// </para>
/// </summary>
public sealed class ConfigDiagnosticCheck : IDiagnosticCheck
{
    /// <inheritdoc />
    public string Name => "config";

    /// <summary>
    /// Runs the check.
    /// </summary>
    /// <param name="cancellationToken">Token observed for cancellation.</param>
    /// <returns>The check outcome.</returns>
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
