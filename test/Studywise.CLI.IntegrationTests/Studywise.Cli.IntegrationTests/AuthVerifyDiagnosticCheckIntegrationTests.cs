using System.Net.Http;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Studywise.Cli.Configuration;
using Studywise.Cli.Diagnostics;
using Studywise.Cli.Diagnostics.Checks;
using Studywise.Cli.Http;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;

namespace Studywise.CLI.IntegrationTests;

[Category("Integration")]
public class AuthVerifyDiagnosticCheckIntegrationTests : BaseIntegrationTest
{
    [Test]
    public async Task AuthVerify_ReturnsPassWhenApiAcceptsKey()
    {
        using var server = WireMockServer.Start();
        server
            .Given(Request.Create().WithPath("/api/v1/auth/verify").UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody("{\"userId\":\"users/abc-123\",\"authMethod\":\"ApiKey\"}"));

        var check = BuildCheck(server.Url!, "test-key");
        var result = await check.RunAsync();

        result.Status.Should().Be(DiagnosticStatus.Pass);
        result.Message.Should().Contain("users/abc-123");
        result.Message.Should().Contain("ApiKey");
    }

    [Test]
    public async Task AuthVerify_ReturnsFailWhenApiRejectsKey()
    {
        using var server = WireMockServer.Start();
        server
            .Given(Request.Create().WithPath("/api/v1/auth/verify").UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(401)
                .WithHeader("Content-Type", "application/json")
                .WithBody("{\"error\":\"InvalidKey\"}"));

        var check = BuildCheck(server.Url!, "bad-key");
        var result = await check.RunAsync();

        result.Status.Should().Be(DiagnosticStatus.Fail);
        result.Message.Should().Contain("invalid or revoked");
    }

    [Test]
    public async Task AuthVerify_ReturnsFailWhenServerErrors()
    {
        using var server = WireMockServer.Start();
        server
            .Given(Request.Create().WithPath("/api/v1/auth/verify").UsingGet())
            .RespondWith(Response.Create().WithStatusCode(503));

        var check = BuildCheck(server.Url!, "test-key");
        var result = await check.RunAsync();

        result.Status.Should().Be(DiagnosticStatus.Fail);
        result.Message.Should().Contain("503");
    }

    [Test]
    public async Task AuthVerify_ReturnsWarnSkipWhenNoKeyConfigured()
    {
        using var server = WireMockServer.Start();
        // No stub - if the check incorrectly calls the API the test will
        // fail with a 404 response from WireMock, which surfaces as a FAIL.
        var check = BuildCheck(server.Url!, null);
        var result = await check.RunAsync();

        result.Status.Should().Be(DiagnosticStatus.Warn);
        result.Message.Should().Contain("SKIP");
        result.Message.Should().Contain("no API key configured");
        server.LogEntries.Should().BeEmpty();
    }

    [Test]
    public async Task Doctor_WithAllChecksPasses_IncludesAuthVerifyInReport()
    {
        using var server = WireMockServer.Start();
        server
            .Given(Request.Create().WithPath("/health").UsingGet())
            .RespondWith(Response.Create().WithStatusCode(200));
        server
            .Given(Request.Create().WithPath("/api/v1/auth/verify").UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody("{\"userId\":\"users/abc-123\",\"authMethod\":\"ApiKey\"}"));

        var configPath = await CreateTempConfigPathAsync("test-key");
        var previousConfigPath = Environment.GetEnvironmentVariable("STUDYWISE_CONFIG_PATH");
        Environment.SetEnvironmentVariable("STUDYWISE_CONFIG_PATH", configPath);

        try
        {
            var services = new ServiceCollection();
            services.AddHttpClient("Studywise", client => client.BaseAddress = new Uri(server.Url!))
                .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
                {
                    AllowAutoRedirect = false
                });
            var sp = services.BuildServiceProvider();
            var factory = sp.GetRequiredService<IHttpClientFactory>();

            var checks = new IDiagnosticCheck[]
            {
                new ConfigDiagnosticCheck(),
                new ApiKeyDiagnosticCheck(),
                new ConnectionDiagnosticCheck(factory),
                new AuthVerifyDiagnosticCheck(new HttpClientStudywiseTransport(factory), new ApplicationConfig { ApiKey = "test-key" })
            };

            var report = await new DiagnosticRunner().RunAsync(checks);

            report.IsSuccess.Should().BeTrue();
            report.Checks.Should().Contain(c => c.Name == "auth-verify" && c.Status == DiagnosticStatus.Pass);
        }
        finally
        {
            Environment.SetEnvironmentVariable("STUDYWISE_CONFIG_PATH", previousConfigPath);
        }
    }

    private static async Task<string> CreateTempConfigPathAsync(string apiKey)
    {
        var dir = Path.Combine(Path.GetTempPath(), $"studywise-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, "config.json");
        await File.WriteAllTextAsync(path, $"{{\"apiKey\":\"{apiKey}\"}}");
        return path;
    }

    private static AuthVerifyDiagnosticCheck BuildCheck(string apiBaseUrl, string? apiKey)
    {
        var services = new ServiceCollection();
        services.AddHttpClient("Studywise", client => client.BaseAddress = new Uri(apiBaseUrl))
            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
            {
                AllowAutoRedirect = false
            });
        var sp = services.BuildServiceProvider();
        var factory = sp.GetRequiredService<IHttpClientFactory>();
        return new AuthVerifyDiagnosticCheck(
            new HttpClientStudywiseTransport(factory),
            new ApplicationConfig { ApiKey = apiKey ?? string.Empty });
    }
}
