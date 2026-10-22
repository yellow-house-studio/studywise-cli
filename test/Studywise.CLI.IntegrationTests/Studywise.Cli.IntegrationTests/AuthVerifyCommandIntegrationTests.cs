using System.Net.Http;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Studywise.Cli.Commands.Auth;
using Studywise.Cli.Configuration;
using Studywise.Cli.Http;
using Studywise.Contracts.Http;
using Studywise.Contracts.Http.Auth;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;

namespace Studywise.CLI.IntegrationTests;

[Category("Integration")]
public class AuthVerifyCommandIntegrationTests : BaseIntegrationTest
{
    [Test]
    public async Task AuthVerify_SucceedsAgainstAKeyTheApiAccepts()
    {
        using var server = WireMockServer.Start();
        server
            .Given(Request.Create()
                .WithPath("/api/v1/auth/verify")
                .UsingGet()
                .WithHeader("X-Studywise-Api-Key", "test-key"))
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody("{\"userId\":\"users/abc-123\",\"authMethod\":\"ApiKey\"}"));

        var console = new TestConsole();
        var handler = BuildHandler(server.Url!, "test-key");

        var exitCode = await handler.HandleAsync(
            new AuthVerifyCommandOptions(Json: false),
            console,
            CancellationToken.None);

        exitCode.Should().Be(0);
        console.Lines.Should().ContainSingle();
        console.Lines[0].Should().Contain("users/abc-123").And.Contain("ApiKey");
    }

    [Test]
    public async Task AuthVerify_WithJson_EmitsTheNormalizedRecord()
    {
        using var server = WireMockServer.Start();
        server
            .Given(Request.Create().WithPath("/api/v1/auth/verify").UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody("{\"userId\":\"users/abc-123\",\"authMethod\":\"ApiKey\"}"));

        var console = new TestConsole();
        var handler = BuildHandler(server.Url!, "test-key");

        var exitCode = await handler.HandleAsync(
            new AuthVerifyCommandOptions(Json: true),
            console,
            CancellationToken.None);

        exitCode.Should().Be(0);
        console.Lines.Should().ContainSingle();
        using var json = JsonDocument.Parse(console.Lines[0]);
        json.RootElement.GetProperty("userId").GetString().Should().Be("users/abc-123");
        json.RootElement.GetProperty("authMethod").GetString().Should().Be("ApiKey");
    }

    [Test]
    public async Task AuthVerify_FailsWhenTheApiRejectsTheKey()
    {
        using var server = WireMockServer.Start();
        server
            .Given(Request.Create().WithPath("/api/v1/auth/verify").UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(401)
                .WithHeader("Content-Type", "application/json")
                .WithBody("{\"error\":\"InvalidKey\"}"));

        var console = new TestConsole();
        var handler = BuildHandler(server.Url!, "bad-key");

        var exitCode = await handler.HandleAsync(
            new AuthVerifyCommandOptions(Json: true),
            console,
            CancellationToken.None);

        exitCode.Should().Be(1);
        console.Lines.Should().BeEmpty();
        console.ErrorOutput.Should().Contain("HTTP 401 Unauthorized");
        console.ErrorOutput.Should().Contain("\"kind\": \"unauthorized\"");
    }

    [Test]
    public async Task AuthVerify_WithNoApiKey_NeverCallsTheApi()
    {
        using var server = WireMockServer.Start();
        var console = new TestConsole();
        var handler = BuildHandler(server.Url!, string.Empty);

        var exitCode = await handler.HandleAsync(
            new AuthVerifyCommandOptions(Json: false),
            console,
            CancellationToken.None);

        exitCode.Should().Be(2);
        server.LogEntries.Should().BeEmpty();
    }

    private static AuthVerifyCommandHandler BuildHandler(string apiBaseUrl, string apiKey)
    {
        var services = new ServiceCollection();
        services.AddStudywiseClient(apiBaseUrl, apiKey);
        services.AddHttpClient<AuthClient>()
            .UseStudywiseDefaults(new ApplicationConfig { ApiKey = apiKey });
        var provider = services.BuildServiceProvider();

        return new AuthVerifyCommandHandler(
            provider.GetRequiredService<AuthClient>(),
            new ApplicationConfig { ApiKey = apiKey, ApiBaseUrl = apiBaseUrl });
    }
}
