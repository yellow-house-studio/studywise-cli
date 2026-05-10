using System.CommandLine;
using System.CommandLine.IO;
using System.Net.Http;
using System.Text.Json;
using Studywise.Cli.Commands;
using Studywise.Cli.Configuration;
using Studywise.Cli.Diagnostics;
using Xunit;

namespace Studywise.Cli.UnitTests;

public class DoctorCommandHandlerTests
{
    [Fact]
    public async Task HandleAsync_WithJson_FallsToJsonReporter()
    {
        var report = new DiagnosticReport(new[]
        {
            new DiagnosticCheckResult("config", DiagnosticStatus.Pass, "Config: OK"),
            new DiagnosticCheckResult("api-key", DiagnosticStatus.Pass, "API-nyckel: OK"),
            new DiagnosticCheckResult("connection", DiagnosticStatus.Pass, "Connection: OK")
        });

        var runner = new FakeDiagnosticRunner(report);
        var httpClientFactory = new FakeHttpClientFactory();
        var config = new ApplicationConfig { ApiKey = "test-key" };
        var handler = new DoctorCommandHandler(runner, httpClientFactory, config);

        var console = new TestConsole();
        var exitCode = await handler.HandleAsync(
            new DoctorCommandOptions(Json: true),
            console,
            CancellationToken.None);

        Assert.Equal(0, exitCode);
        var output = console.Lines.Single();
        using var json = JsonDocument.Parse(output);
        Assert.Equal("config", json.RootElement.GetProperty("checks")[0].GetProperty("name").GetString());
    }

    [Fact]
    public async Task HandleAsync_WithoutJson_FallsToTextFormatter()
    {
        var report = new DiagnosticReport(new[]
        {
            new DiagnosticCheckResult("config", DiagnosticStatus.Pass, "Config: OK")
        });

        var runner = new FakeDiagnosticRunner(report);
        var httpClientFactory = new FakeHttpClientFactory();
        var config = new ApplicationConfig();
        var handler = new DoctorCommandHandler(runner, httpClientFactory, config);

        var console = new TestConsole();
        var exitCode = await handler.HandleAsync(
            new DoctorCommandOptions(Json: false),
            console,
            CancellationToken.None);

        Assert.Equal(0, exitCode);
        Assert.Contains("Studywise CLI Diagnostics", console.Lines[0]);
    }

    [Fact]
    public async Task HandleAsync_WithFailingChecks_ReturnsExitCode1()
    {
        var report = new DiagnosticReport(new[]
        {
            new DiagnosticCheckResult("api-key", DiagnosticStatus.Fail, "API-nyckel: FAIL")
        });

        var runner = new FakeDiagnosticRunner(report);
        var httpClientFactory = new FakeHttpClientFactory();
        var config = new ApplicationConfig();
        var handler = new DoctorCommandHandler(runner, httpClientFactory, config);

        var console = new TestConsole();
        var exitCode = await handler.HandleAsync(
            new DoctorCommandOptions(Json: false),
            console,
            CancellationToken.None);

        Assert.Equal(1, exitCode);
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

    private sealed class TestConsole : IConsole
    {
        public List<string> Lines { get; } = [];

        public IStandardStreamWriter Out => new TestStreamWriter(Lines);
        public IStandardStreamWriter Error => new TestStreamWriter([]);
        public bool IsOutputRedirected => false;
        public bool IsErrorRedirected => false;
        public bool IsInputRedirected => false;
    }

    private sealed class TestStreamWriter : IStandardStreamWriter
    {
        private readonly List<string> _lines;

        public TestStreamWriter(List<string> lines) => _lines = lines;

        public void Write(string? value)
        {
            if (!string.IsNullOrEmpty(value))
                _lines.Add(value);
        }

        public void WriteLine(string? value) => _lines.Add(value ?? string.Empty);

        public void Write(char[] buffer, int index, int count)
        {
            if (count > 0)
                _lines.Add(new string(buffer, index, count));
        }
    }
}