using System.CommandLine;
using System.CommandLine.Builder;
using System.CommandLine.Parsing;
using Microsoft.Extensions.DependencyInjection;
using Studywise.Cli.Commands;
using Studywise.Cli.Commands.Auth;
using Studywise.Cli.Commands.Doctor;
using Studywise.Cli.Configuration;
using Studywise.Cli.Diagnostics;
using Studywise.Cli.Http;
using Studywise.Contracts.Http;
using Studywise.Contracts.Http.Auth;

var config = ApplicationConfig.FromEnvironment();

var services = new ServiceCollection();

// The SDK is the CLI's only abstraction for the Studywise API
// (docs/design/cli-design.md § 6). It owns the base address, the timeout
// and the X-Studywise-Api-Key header.
services.AddStudywiseClient(
    config.ApiBaseUrl,
    string.IsNullOrWhiteSpace(config.ApiKey) ? null : config.ApiKey);

// The SDK's AddStudywiseClient exposes no hook for the primary message
// handler, so the CLI re-registers the clients it uses on top of the SDK's
// registration to apply its own defaults: the User-Agent, and refusing to
// follow redirects (the SDK sends the API key on every request, so a
// followed 3xx would forward the credential to another host).
services.AddHttpClient<AuthClient>().UseStudywiseDefaults(config);

// Raw named client for the `connection` check's /health probe. The SDK does
// not ship a health endpoint yet (studywise-api#891), so this one probe
// still uses a plain HttpClient.
services.AddHttpClient(StudywiseDefaults.ApiName, client =>
{
    client.BaseAddress = new Uri(config.ApiBaseUrl);
    client.DefaultRequestHeaders.Add("User-Agent", config.UserAgent);
})
.ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
{
    // Disabled so X-Studywise-Api-Key cannot leak across a 3xx to a different host.
    AllowAutoRedirect = false
});

services.AddSingleton(config);
services.AddSingleton<DiagnosticRunner>();
services.AddSingleton<IDiagnosticRunner>(sp => sp.GetRequiredService<DiagnosticRunner>());

services.AddSingleton<Command, DoctorCommand>();
services.AddTransient<ICommandHandler<DoctorCommandOptions>, DoctorCommandHandler>();

services.AddSingleton<Command, AuthCommand>();
services.AddTransient<ICommandHandler<AuthVerifyCommandOptions>, AuthVerifyCommandHandler>();

var serviceProvider = services.BuildServiceProvider();

var commands = serviceProvider.GetServices<Command>().ToList();

var rootCommand = new RootCommand("Studywise CLI - Client CLI for agents and end users");
foreach (var command in commands)
{
    rootCommand.AddCommand(command);
}

var parser = new CommandLineBuilder(rootCommand)
    .UseDefaults()
    .UseDependencyInjection(serviceProvider, services)
    .Build();

return await parser.InvokeAsync(args, null);
