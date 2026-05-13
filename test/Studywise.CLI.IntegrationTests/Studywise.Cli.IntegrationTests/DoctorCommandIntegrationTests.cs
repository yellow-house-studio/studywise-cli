using System.Net.Http;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Studywise.Cli.Configuration;
using Studywise.Cli.Diagnostics;
using Studywise.Cli.Diagnostics.Checks;
using Studywise.Cli.Diagnostics.Formatting;
using Studywise.Cli.Formatting;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;

namespace Studywise.CLI.IntegrationTests;

public class DoctorCommandIntegrationTests
{
    [Fact]
    public async Task Doctor_TextFormatting_ProducesReadableText()
    {
        using var server = WireMockServer.Start();
        server
            .Given(Request.Create().WithPath("/health").UsingGet())
            .RespondWith(Response.Create().WithStatusCode(200));

        var report = await RunDoctorDiagnosticsAsync(server.Url!, "test-key");
        var output = new TextDiagnosticReportFormatter().Format(report);

        Assert.Contains("Studywise CLI Diagnostics", output);
        Assert.Contains("Config:", output);
        Assert.Contains("API-nyckel:", output);
        Assert.Contains("Connection:", output);
        Assert.Contains("All checks passed", output);
    }

    [Fact]
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

        Assert.True(root.TryGetProperty("generatedAtUtc", out var generatedAtUtc));
        Assert.Equal(JsonValueKind.String, generatedAtUtc.ValueKind);
        Assert.True(DateTimeOffset.TryParse(generatedAtUtc.GetString(), out _));

        Assert.True(root.TryGetProperty("checks", out var checks));
        Assert.Equal(JsonValueKind.Array, checks.ValueKind);
        Assert.Equal(3, checks.GetArrayLength());
        Assert.Equal("config", checks[0].GetProperty("name").GetString());
        Assert.Equal("api-key", checks[1].GetProperty("name").GetString());
        Assert.Equal("connection", checks[2].GetProperty("name").GetString());
        Assert.Contains(checks[0].GetProperty("status").GetString(), new[] { "pass", "warn", "fail" });
        Assert.Contains(checks[1].GetProperty("status").GetString(), new[] { "pass", "warn", "fail" });
        Assert.Contains(checks[2].GetProperty("status").GetString(), new[] { "pass", "warn", "fail" });
        Assert.True(checks[0].TryGetProperty("message", out var configMessage));
        Assert.False(string.IsNullOrWhiteSpace(configMessage.GetString()));
        Assert.True(checks[1].TryGetProperty("message", out var apiKeyMessage));
        Assert.False(string.IsNullOrWhiteSpace(apiKeyMessage.GetString()));
        Assert.True(checks[2].TryGetProperty("message", out var connectionMessage));
        Assert.False(string.IsNullOrWhiteSpace(connectionMessage.GetString()));
        var passedCount = root.GetProperty("passedCount").GetInt32();
        var failedCount = root.GetProperty("failedCount").GetInt32();
        var warningCount = root.GetProperty("warningCount").GetInt32();

        Assert.Equal(3, passedCount + failedCount + warningCount);
        Assert.Equal(0, failedCount);
        Assert.True(root.GetProperty("isSuccess").GetBoolean());
    }

    [Fact]
    public async Task Doctor_ReturnsFailureWhenApiKeyIsMissing()
    {
        using var server = WireMockServer.Start();
        server
            .Given(Request.Create().WithPath("/health").UsingGet())
            .RespondWith(Response.Create().WithStatusCode(200));

        var report = await RunDoctorDiagnosticsAsync(server.Url!);
        var output = new TextDiagnosticReportFormatter().Format(report);

        Assert.False(report.IsSuccess);
        Assert.Equal(1, report.FailedCount);
        Assert.Contains("API-nyckel: FAIL", output);
    }

    [Fact]
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

        Assert.Equal(DiagnosticStatus.Fail, connection.Status);
        Assert.Contains("/health returned 302", connection.Message);
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
                new ConnectionDiagnosticCheck(httpClientFactory)
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
