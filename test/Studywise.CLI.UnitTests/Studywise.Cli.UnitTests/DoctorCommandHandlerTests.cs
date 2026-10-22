using System.CommandLine;
using System.Net.Http;
using System.Text.Json;
using Studywise.Cli.Commands.Doctor;
using Studywise.Cli.Configuration;
using Studywise.Cli.Diagnostics;
using Studywise.Contracts.Http.Auth;

namespace Studywise.CLI.UnitTests;

[Category("Unit")]
public class DoctorCommandHandlerTests
{
    [Test]
    public async Task HandleAsync_WithJson_FallsToJsonReporter()
    {
        var report = new DiagnosticReport(new[]
        {
            new DiagnosticCheckResult("config", DiagnosticStatus.Pass, "Config: OK"),
            new DiagnosticCheckResult("api-key", DiagnosticStatus.Pass, "API key: OK"),
            new DiagnosticCheckResult("connection", DiagnosticStatus.Pass, "Connection: OK")
        });

        var runner = new FakeDiagnosticRunner(report);
        var httpClientFactory = new FakeHttpClientFactory();
        var authClient = BuildAuthClient();
        var config = new ApplicationConfig { ApiKey = "test-key" };
        var handler = new DoctorCommandHandler(runner, httpClientFactory, authClient, config);

        var console = new TestConsole();
        var exitCode = await handler.HandleAsync(
            new DoctorCommandOptions(Json: true),
            console,
            CancellationToken.None);

        exitCode.Should().Be(0);
        var output = console.Lines.Single();
        using var json = JsonDocument.Parse(output);
        json.RootElement.GetProperty("checks")[0].GetProperty("name").GetString().Should().Be("config");
    }

    [Test]
    public async Task HandleAsync_WithoutJson_FallsToTextFormatter()
    {
        var report = new DiagnosticReport(new[]
        {
            new DiagnosticCheckResult("config", DiagnosticStatus.Pass, "Config: OK")
        });

        var runner = new FakeDiagnosticRunner(report);
        var httpClientFactory = new FakeHttpClientFactory();
        var authClient = BuildAuthClient();
        var config = new ApplicationConfig();
        var handler = new DoctorCommandHandler(runner, httpClientFactory, authClient, config);

        var console = new TestConsole();
        var exitCode = await handler.HandleAsync(
            new DoctorCommandOptions(Json: false),
            console,
            CancellationToken.None);

        exitCode.Should().Be(0);
        console.Lines[0].Should().Contain("Studywise CLI Diagnostics");
    }

    [Test]
    public async Task HandleAsync_WithFailingChecks_ReturnsExitCode1()
    {
        var report = new DiagnosticReport(new[]
        {
            new DiagnosticCheckResult("api-key", DiagnosticStatus.Fail, "API key: FAIL")
        });

        var runner = new FakeDiagnosticRunner(report);
        var httpClientFactory = new FakeHttpClientFactory();
        var authClient = BuildAuthClient();
        var config = new ApplicationConfig();
        var handler = new DoctorCommandHandler(runner, httpClientFactory, authClient, config);

        var console = new TestConsole();
        var exitCode = await handler.HandleAsync(
            new DoctorCommandOptions(Json: false),
            console,
            CancellationToken.None);

        exitCode.Should().Be(1);
    }

    [TestCase("config", "config")]
    [TestCase("api-key", "api-key")]
    [TestCase("connection", "connection")]
    [TestCase("auth-verify", "auth-verify")]
    [TestCase("CONFIG", "config")]
    [TestCase("Api-Key", "api-key")]
    public async Task HandleAsync_WithCheckName_PassesOnlySelectedCheck(
        string checkName,
        string expectedCheckName)
    {
        var httpClientFactory = new FakeHttpClientFactory();
        var authClient = BuildAuthClient();
        var config = new ApplicationConfig();
        var runner = new TrackingDiagnosticRunner();
        var handler = new DoctorCommandHandler(runner, httpClientFactory, authClient, config);

        var console = new TestConsole();
        var exitCode = await handler.HandleAsync(
            new DoctorCommandOptions(Json: false, CheckName: checkName),
            console,
            CancellationToken.None);

        runner.CapturedChecks.Should().HaveCount(1);
        runner.CapturedChecks[0].Name.Should().Be(expectedCheckName);
    }

    [Test]
    public async Task HandleAsync_WithCheckNameAll_PassesAllFourChecks()
    {
        var httpClientFactory = new FakeHttpClientFactory();
        var authClient = BuildAuthClient();
        var config = new ApplicationConfig();
        var runner = new TrackingDiagnosticRunner();
        var handler = new DoctorCommandHandler(runner, httpClientFactory, authClient, config);

        var console = new TestConsole();
        var exitCode = await handler.HandleAsync(
            new DoctorCommandOptions(Json: false, CheckName: "all"),
            console,
            CancellationToken.None);

        runner.CapturedChecks.Should().HaveCount(4);
        runner.CapturedChecks[0].Name.Should().Be("config");
        runner.CapturedChecks[1].Name.Should().Be("api-key");
        runner.CapturedChecks[2].Name.Should().Be("connection");
        runner.CapturedChecks[3].Name.Should().Be("auth-verify");
    }

    [Test]
    public async Task HandleAsync_WithUnknownCheckName_ReturnsExitCode1AndWritesToError()
    {
        var httpClientFactory = new FakeHttpClientFactory();
        var authClient = BuildAuthClient();
        var config = new ApplicationConfig();
        var runner = new FakeDiagnosticRunner(new DiagnosticReport([]));
        var handler = new DoctorCommandHandler(runner, httpClientFactory, authClient, config);

        var console = new TestConsole();
        var exitCode = await handler.HandleAsync(
            new DoctorCommandOptions(Json: false, CheckName: "foo"),
            console,
            CancellationToken.None);

        exitCode.Should().Be(1);
        console.ErrorOutput.Trim().Should().Be("unknown check: foo");
    }

    private sealed class FakeDiagnosticRunner : IDiagnosticRunner
    {
        private readonly DiagnosticReport _report;

        public FakeDiagnosticRunner(DiagnosticReport report) => _report = report;

        public Task<DiagnosticReport> RunAsync(
            IEnumerable<IDiagnosticCheck> checks,
            CancellationToken cancellationToken = default)
            => Task.FromResult(_report);
    }

    private sealed class FakeHttpClientFactory : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new HttpClient();
    }

    private static AuthClient BuildAuthClient()
        => new(new HttpClient(new StubHttpMessageHandler((_, _) =>
                Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK))))
        {
            BaseAddress = new Uri("https://api.studywise.test")
        });

    private sealed class TrackingDiagnosticRunner : IDiagnosticRunner
    {
        public IDiagnosticCheck[] CapturedChecks { get; private set; } = [];

        public Task<DiagnosticReport> RunAsync(
            IEnumerable<IDiagnosticCheck> checks,
            CancellationToken cancellationToken = default)
        {
            CapturedChecks = checks.ToArray();
            return Task.FromResult(new DiagnosticReport(
                CapturedChecks.Select(c => new DiagnosticCheckResult(c.Name, DiagnosticStatus.Fail, "test")).ToArray()));
        }
    }

}
