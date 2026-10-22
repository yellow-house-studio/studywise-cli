using System.Net.Http;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Studywise.Cli.Configuration;
using Studywise.Cli.Diagnostics;
using Studywise.Cli.Diagnostics.Checks;
using Studywise.Cli.Http;
using Studywise.Contracts.Http;
using Studywise.Contracts.Http.Auth;
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
    public async Task AuthVerify_SendsTheApiKeyHeader()
    {
        using var server = WireMockServer.Start();
        // The stub only matches when the SDK attached the API key header. A
        // missing header produces a 404, which the check reports as a failure.
        server
            .Given(Request.Create()
                .WithPath("/api/v1/auth/verify")
                .UsingGet()
                .WithHeader("X-Studywise-Api-Key", "test-key"))
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody("{\"userId\":\"users/abc-123\",\"authMethod\":\"ApiKey\"}"));

        var check = BuildCheck(server.Url!, "test-key");
        var result = await check.RunAsync();

        result.Status.Should().Be(DiagnosticStatus.Pass);
        result.Message.Should().Contain("users/abc-123");
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
    public async Task AuthVerify_DoesNotFollowRedirects_SoTheApiKeyIsNotForwarded()
    {
        using var server = WireMockServer.Start();
        server
            .Given(Request.Create().WithPath("/api/v1/auth/verify").UsingGet())
            .RespondWith(Response.Create().WithStatusCode(302).WithHeader("Location", "/redirect-target"));
        server
            .Given(Request.Create().WithPath("/redirect-target").UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody("{\"userId\":\"users/redirected\",\"authMethod\":\"ApiKey\"}"));

        var check = BuildCheck(server.Url!, "test-key");
        var result = await check.RunAsync();

        result.Status.Should().Be(DiagnosticStatus.Fail);
        result.Message.Should().Contain("server returned 302");
        server.LogEntries
            .Where(entry => entry.RequestMessage is not null)
            .Select(entry => entry.RequestMessage!.Path)
            .Should().NotContain("/redirect-target");
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
            services.AddStudywiseClient(server.Url!, "test-key");
            services.AddHttpClient<AuthClient>().UseStudywiseDefaults(new ApplicationConfig { ApiKey = "test-key" });
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
                new AuthVerifyDiagnosticCheck(sp.GetRequiredService<AuthClient>(), new ApplicationConfig { ApiKey = "test-key" })
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
        services.AddStudywiseClient(apiBaseUrl, apiKey);
        services.AddHttpClient<AuthClient>().UseStudywiseDefaults(new ApplicationConfig { ApiKey = apiKey ?? string.Empty });
        var sp = services.BuildServiceProvider();
        return new AuthVerifyDiagnosticCheck(
            sp.GetRequiredService<AuthClient>(),
            new ApplicationConfig { ApiKey = apiKey ?? string.Empty });
    }
}
