namespace Studywise.Cli.Diagnostics.Checks;

public sealed class ApiKeyDiagnosticCheck : IDiagnosticCheck
{
    private const string ApiKeyEnvironmentVariable = "STUDYWISE_API_KEY";

    public string Name => "api-key";

    public Task<DiagnosticCheckResult> RunAsync(CancellationToken cancellationToken = default)
    {
        var apiKey = Environment.GetEnvironmentVariable(ApiKeyEnvironmentVariable);

        if (!string.IsNullOrWhiteSpace(apiKey))
        {
            return Task.FromResult(new DiagnosticCheckResult(Name, DiagnosticStatus.Pass, "API-nyckel: OK — finns (maskerad)"));
        }

        return Task.FromResult(new DiagnosticCheckResult(Name, DiagnosticStatus.Fail, "API-nyckel: FAIL — saknas i config"));
    }
}
