using Studywise.Cli.Auth;

namespace Studywise.Cli.Diagnostics.Checks;

public sealed class ApiKeyDiagnosticCheck(ITokenProvider tokenProvider) : IDiagnosticCheck
{
    public string Name => "api-key";

    public Task<DiagnosticCheckResult> RunAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            tokenProvider.GetToken();
            return Task.FromResult(new DiagnosticCheckResult(Name, DiagnosticStatus.Pass, "API-nyckel: OK — finns (maskerad)"));
        }
        catch (InvalidOperationException)
        {
            return Task.FromResult(new DiagnosticCheckResult(Name, DiagnosticStatus.Fail, "API-nyckel: FAIL — saknas i environment variable"));
        }
    }
}
