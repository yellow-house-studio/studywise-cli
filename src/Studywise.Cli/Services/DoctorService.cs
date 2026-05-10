using Studywise.Cli.Configuration;
using Studywise.Cli.Diagnostics;
using Studywise.Cli.Diagnostics.Checks;

namespace Studywise.Cli.Services;

public sealed class DoctorService(IHttpClientFactory httpClientFactory)
{
    public async Task<DiagnosticReport> RunDiagnosticsAsync(CancellationToken cancellationToken = default)
    {
        var config = ApplicationConfig.FromEnvironment();
        
        var checks = new IDiagnosticCheck[]
        {
            new ConfigDiagnosticCheck(),
            new ApiKeyDiagnosticCheck(config),
            new ConnectionDiagnosticCheck(httpClientFactory)
        };

        var runner = new DiagnosticRunner();
        return await runner.RunAsync(checks, cancellationToken);
    }
}