using System.CommandLine;
using System.CommandLine.IO;
using System.Net.Http;
using System.Text.Json;
using Studywise.Cli.Commands.Doctor;
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

    [Theory]
    [InlineData("config", "config")]
    [InlineData("api-key", "api-key")]
    [InlineData("connection", "connection")]
    [InlineData("CONFIG", "config")]
    [InlineData("Api-Key", "api-key")]
    public async Task HandleAsync_WithCheckName_PassesOnlySelectedCheck(
        string checkName,
        string expectedCheckName)
    {
        var httpClientFactory = new FakeHttpClientFactory();
        var config = new ApplicationConfig();
        var runner = new TrackingDiagnosticRunner();
        var handler = new DoctorCommandHandler(runner, httpClientFactory, config);

        var console = new TestConsole();
        var exitCode = await handler.HandleAsync(
            new DoctorCommandOptions(Json: false, CheckName: checkName),
            console,
            CancellationToken.None);

        Assert.Single(runner.CapturedChecks);
        Assert.Equal(expectedCheckName, runner.CapturedChecks[0].Name);
    }

    [Fact]
    public async Task HandleAsync_WithCheckNameAll_PassesAllThreeChecks()
    {
        var httpClientFactory = new FakeHttpClientFactory();
        var config = new ApplicationConfig();
        var runner = new TrackingDiagnosticRunner();
        var handler = new DoctorCommandHandler(runner, httpClientFactory, config);

        var console = new TestConsole();
        var exitCode = await handler.HandleAsync(
            new DoctorCommandOptions(Json: false, CheckName: "all"),
            console,
            CancellationToken.None);

        Assert.Equal(3, runner.CapturedChecks.Length);
        Assert.Equal("config", runner.CapturedChecks[0].Name);
        Assert.Equal("api-key", runner.CapturedChecks[1].Name);
        Assert.Equal("connection", runner.CapturedChecks[2].Name);
    }

    [Fact]
    public async Task HandleAsync_WithUnknownCheckName_ReturnsExitCode1AndWritesToError()
    {
        var httpClientFactory = new FakeHttpClientFactory();
        var config = new ApplicationConfig();
        var runner = new FakeDiagnosticRunner(new DiagnosticReport([]));
        var handler = new DoctorCommandHandler(runner, httpClientFactory, config);

        var console = new TestConsole();
        var exitCode = await handler.HandleAsync(
            new DoctorCommandOptions(Json: false, CheckName: "foo"),
            console,
            CancellationToken.None);

        Assert.Equal(1, exitCode);
        Assert.Equal("unknown check: foo", console.ErrorOutput.Trim());
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

    private sealed class TestConsole : IConsole
    {
        public List<string> Lines { get; } = [];
        public StringWriter ErrorWriter { get; } = new StringWriter();
        public string ErrorOutput => ErrorWriter.ToString();

        public IStandardStreamWriter Out => new TestStreamWriter(Lines);
        public IStandardStreamWriter Error => new StringWriterStreamWriter(ErrorWriter);
        public bool IsOutputRedirected => false;
        public bool IsErrorRedirected => false;
        public bool IsInputRedirected => false;
    }

    private sealed class StringWriterStreamWriter : IStandardStreamWriter
    {
        private readonly TextWriter _writer;

        public StringWriterStreamWriter(TextWriter writer) => _writer = writer;

        public void Write(string? value) => _writer.Write(value ?? string.Empty);
        public void WriteLine(string? value) => _writer.WriteLine(value ?? string.Empty);
        public void Write(char[] buffer, int index, int count) => _writer.Write(buffer, index, count);
    }

    private sealed class TestStreamWriter : IStandardStreamWriter
    {
        private readonly Action<string>? _onWrite;
        private readonly List<string>? _lines;

        public TestStreamWriter(List<string> lines) => _lines = lines;
        public TestStreamWriter(Action<string> onWrite) => _onWrite = onWrite;

        public void Write(string? value)
        {
            if (string.IsNullOrEmpty(value)) return;
            _lines?.Add(value);
            _onWrite?.Invoke(value);
        }

        public void WriteLine(string? value)
        {
            var line = value ?? string.Empty;
            _lines?.Add(line);
            _onWrite?.Invoke(line + Environment.NewLine);
        }

        public void Write(char[] buffer, int index, int count)
        {
            if (count > 0)
            {
                var s = new string(buffer, index, count);
                _lines?.Add(s);
                _onWrite?.Invoke(s);
            }
        }
    }
}