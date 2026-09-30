using System.Net.Http;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Studywise.Cli.Configuration;
using Studywise.Cli.Diagnostics;
using Studywise.Cli.Diagnostics.Checks;
using Studywise.Cli.Diagnostics.Formatting;
using Studywise.Cli.Formatting;
using Studywise.Cli.Http;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;

namespace Studywise.CLI.IntegrationTests;

[Category("Integration")]
public class DoctorCommandIntegrationTests : BaseIntegrationTest
{
    [Test]
    public async Task Doctor_TextFormatting_ProducesReadableText()
    {
        using var server = WireMockServer.Start();
        server
            .Given(Request.Create().WithPath("/health").UsingGet())
            .RespondWith(Response.Create().WithStatusCode(200));

        var report = await RunDoctorDiagnosticsAsync(server.Url!, "test-key");
        var output = new TextDiagnosticReportFormatter().Format(report);

        output.Should().Contain("Studywise CLI Diagnostics");
        output.Should().Contain("Config:");
        output.Should().Contain("API key:");
        output.Should().Contain("Connection:");
        output.Should().Contain("All checks passed");
    }

    [Test]
    public async Task Doctor_JsonFormatting_ProducesJsonReport()
    {
        using var server = WireMockServer.Start();
        server
            .Given(Request.Create().WithPath("/health").UsingGet())
            .RespondWith(Response.Create().WithStatusCode(200));

        var report = await RunDoctorDiagnosticsAsync(server.Url!, "test-key");
        var output = JsonReporter.Format(report);

        using var json = JsonDocument.Parse(output);
        var root = json.RootElement;

        root.TryGetProperty("generatedAtUtc", out var generatedAtUtc).Should().BeTrue();
        generatedAtUtc.ValueKind.Should().Be(JsonValueKind.String);
        DateTimeOffset.TryParse(generatedAtUtc.GetString(), out _).Should().BeTrue();

        root.TryGetProperty("checks", out var checks).Should().BeTrue();
        checks.ValueKind.Should().Be(JsonValueKind.Array);
        checks.GetArrayLength().Should().Be(4);
        checks[0].GetProperty("name").GetString().Should().Be("config");
        checks[1].GetProperty("name").GetString().Should().Be("api-key");
        checks[2].GetProperty("name").GetString().Should().Be("connection");
        checks[3].GetProperty("name").GetString().Should().Be("auth-verify");
        new[] { "pass", "warn", "fail" }.Should().Contain(checks[0].GetProperty("status").GetString());
        new[] { "pass", "warn", "fail" }.Should().Contain(checks[1].GetProperty("status").GetString());
        new[] { "pass", "warn", "fail" }.Should().Contain(checks[2].GetProperty("status").GetString());
        checks[0].TryGetProperty("message", out var configMessage).Should().BeTrue();
        configMessage.GetString().Should().NotBeNullOrWhiteSpace();
        checks[1].TryGetProperty("message", out var apiKeyMessage).Should().BeTrue();
        apiKeyMessage.GetString().Should().NotBeNullOrWhiteSpace();
        checks[2].TryGetProperty("message", out var connectionMessage).Should().BeTrue();
        connectionMessage.GetString().Should().NotBeNullOrWhiteSpace();
        var passedCount = root.GetProperty("passedCount").GetInt32();
        var failedCount = root.GetProperty("failedCount").GetInt32();
        var warningCount = root.GetProperty("warningCount").GetInt32();

        (passedCount + failedCount + warningCount).Should().Be(4);
        failedCount.Should().Be(0);
        root.GetProperty("isSuccess").GetBoolean().Should().BeTrue();
    }

    [Test]
    public async Task Doctor_ReturnsFailureWhenApiKeyIsMissing()
    {
        using var server = WireMockServer.Start();
        server
            .Given(Request.Create().WithPath("/health").UsingGet())
            .RespondWith(Response.Create().WithStatusCode(200));

        var report = await RunDoctorDiagnosticsAsync(server.Url!);
        var output = new TextDiagnosticReportFormatter().Format(report);

        report.IsSuccess.Should().BeFalse();
        report.FailedCount.Should().Be(1);
        output.Should().Contain("API key: FAIL");
    }

    [Test]
    public async Task Doctor_ConnectionCheck_FailsWhenHealthRedirectsMoreThanOnce()
    {
        using var server = WireMockServer.Start();
        server
            .Given(Request.Create().WithPath("/health").UsingGet())
            .RespondWith(Response.Create().WithStatusCode(302).WithHeader("Location", "/redirect-1"));
        server
            .Given(Request.Create().WithPath("/redirect-1").UsingGet())
            .RespondWith(Response.Create().WithStatusCode(302).WithHeader("Location", "/redirect-2"));
        server
            .Given(Request.Create().WithPath("/redirect-2").UsingGet())
            .RespondWith(Response.Create().WithStatusCode(200));

        var report = await RunDoctorDiagnosticsAsync(server.Url!, "test-key");
        var connection = report.Checks.Single(check => check.Name == "connection");

        connection.Status.Should().Be(DiagnosticStatus.Fail);
        connection.Message.Should().Contain("/health returned 302");
    }

    private static async Task<DiagnosticReport> RunDoctorDiagnosticsAsync(string apiBaseUrl, string? apiKey = null)
    {
        var previousBaseUrl = Environment.GetEnvironmentVariable("STUDYWISE_API_BASE_URL");
        var previousApiKey = Environment.GetEnvironmentVariable("STUDYWISE_API_KEY");
        var previousConfigPath = Environment.GetEnvironmentVariable("STUDYWISE_CONFIG_PATH");

        var configDirectory = Path.Combine(Path.GetTempPath(), $"studywise-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(configDirectory);

        var configPath = Path.Combine(configDirectory, "config.json");
        if (!string.IsNullOrWhiteSpace(apiKey))
        {
            await File.WriteAllTextAsync(configPath, $"{{\"apiKey\":\"{apiKey}\"}}");
        }
        else
        {
            await File.WriteAllTextAsync(configPath, "{}");
        }

        var services = new ServiceCollection();
        services.AddHttpClient("Studywise", client => client.BaseAddress = new Uri(apiBaseUrl))
            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
            {
                AllowAutoRedirect = true,
                MaxAutomaticRedirections = 1
            });
        var serviceProvider = services.BuildServiceProvider();
        var httpClientFactory = serviceProvider.GetRequiredService<IHttpClientFactory>();

        try
        {
            Environment.SetEnvironmentVariable("STUDYWISE_API_BASE_URL", apiBaseUrl);
            Environment.SetEnvironmentVariable("STUDYWISE_API_KEY", apiKey);
            Environment.SetEnvironmentVariable("STUDYWISE_CONFIG_PATH", configPath);

            var checks = new IDiagnosticCheck[]
            {
                new ConfigDiagnosticCheck(),
                new ApiKeyDiagnosticCheck(),
                new ConnectionDiagnosticCheck(httpClientFactory),
                new AuthVerifyDiagnosticCheck(new HttpClientStudywiseTransport(httpClientFactory), new ApplicationConfig { ApiKey = apiKey ?? string.Empty })
            };

            return await new DiagnosticRunner().RunAsync(checks);
        }
        finally
        {
            Environment.SetEnvironmentVariable("STUDYWISE_API_BASE_URL", previousBaseUrl);
            Environment.SetEnvironmentVariable("STUDYWISE_API_KEY", previousApiKey);
            Environment.SetEnvironmentVariable("STUDYWISE_CONFIG_PATH", previousConfigPath);

            if (Directory.Exists(configDirectory))
            {
                Directory.Delete(configDirectory, recursive: true);
            }
        }
    }
}
