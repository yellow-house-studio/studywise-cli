using System.CommandLine;
using System.CommandLine.Builder;
using System.CommandLine.Parsing;
using Microsoft.Extensions.DependencyInjection;
using Studywise.Cli.Commands;
using Studywise.Cli.Commands.Doctor;
using Studywise.Cli.Configuration;
using Studywise.Cli.Diagnostics;

var config = ApplicationConfig.FromEnvironment();

var services = new ServiceCollection();

services.AddHttpClient(StudywiseDefaults.ApiName, client =>
{
    client.BaseAddress = new Uri(config.ApiBaseUrl);
    client.DefaultRequestHeaders.Add("User-Agent", config.UserAgent);
})
.ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
{
    AllowAutoRedirect = true,
    MaxAutomaticRedirections = 1
});

services.AddSingleton(config);
services.AddSingleton<DiagnosticRunner>();
services.AddSingleton<IDiagnosticRunner>(sp => sp.GetRequiredService<DiagnosticRunner>());

services.AddSingleton<Command, DoctorCommand>();
services.AddTransient<ICommandHandler<DoctorCommandOptions>, DoctorCommandHandler>();

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
