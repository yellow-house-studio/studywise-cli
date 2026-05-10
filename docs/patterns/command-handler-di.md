# CLI Command Handler Pattern with Dependency Injection

## Context

This CLI is built with `System.CommandLine` 2.0.0-beta4.22272.1 and `Microsoft.Extensions.DependencyInjection`. Commands are the first-class unit of the application. A clean separation between CLI parsing and command behavior is required so units can be tested independently and the pattern scales to future commands without accumulating coupling.

## Problem

`System.CommandLine` has a built-in pseudo `IServiceProvider` accessible via `InvocationContext.BindingContext`. This provider is backed by a dictionary of factories, not by the full Microsoft DI container. As a result, services registered in the application DI container are not available to command handlers unless we explicitly bridge the two.

The original approach used static factory methods (`Create()`) on command classes and manually resolved services from `context.BindingContext.GetRequiredService<T>()`. This works for simple cases but breaks down as commands grow, because:

- Commands own both CLI shape and behavior, making unit testing harder.
- Static command creation makes DI injection of dependencies awkward.
- No reusable testing seam between "command parses CLI" and "handler does work".
- Mixing `System.CommandLine` types into business logic pollutes the domain.

## Solution

Introduce a three-part pattern per command:

1. **CommandOptions POCO** — parsed CLI arguments as a simple record.
2. **Command subclass** — only CLI shape: name, description, options, parsing, handler dispatch.
3. **CommandHandler** — concrete class implementing `ICommandHandler<TOptions>`, owning all behavior and accepting injected dependencies.

A shared `ICommandHandler<in TOptions>` interface provides the consistent testing seam.

### Interface

```csharp
public interface ICommandHandler<in TOptions>
{
    Task<int> HandleAsync(
        TOptions options,
        IConsole console,
        CancellationToken cancellationToken);
}
```

`TOptions` is always a record type capturing parsed CLI values. The interface is intentionally small: the handler is the behavioral unit, not a framework abstraction.

### CommandOptions

Each command defines its options as a record:

```csharp
public sealed record DoctorCommandOptions(bool Json);
```

This gives us a strongly-typed, immutable representation of everything the user passed on the command line.

### Command Class

Commands inherit `System.CommandLine.Command` and declare only CLI surface:

```csharp
public sealed class DoctorCommand : Command
{
    private readonly Option<bool> _jsonOption;

    public DoctorCommand(ICommandHandler<DoctorCommandOptions> handler)
        : base("doctor", "Run CLI diagnostics checks")
    {
        _jsonOption = new Option<bool>("--json", "Output diagnostics as JSON");
        AddOption(_jsonOption);

        SetHandler(async context =>
        {
            var options = new DoctorCommandOptions(
                Json: context.ParseResult.GetValueForOption(_jsonOption));

            context.ExitCode = await handler.HandleAsync(
                options,
                context.Console,
                context.GetCancellationToken());
        });
    }
}
```

Key design decisions:

- Constructor takes the handler interface, resolved from DI.
- No business logic; only maps `ParseResult` to `CommandOptions`.
- `SetHandler` delegates to the injected handler.
- All `System.CommandLine` types stay in the command class.

### CommandHandler

Concrete handler implementing the interface:

```csharp
public sealed class DoctorCommandHandler : ICommandHandler<DoctorCommandOptions>
{
    private readonly DiagnosticRunner _runner;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ApplicationConfig _config;

    public DoctorCommandHandler(
        DiagnosticRunner runner,
        IHttpClientFactory httpClientFactory,
        ApplicationConfig config)
    {
        _runner = runner;
        _httpClientFactory = httpClientFactory;
        _config = config;
    }

    public async Task<int> HandleAsync(
        DoctorCommandOptions options,
        IConsole console,
        CancellationToken cancellationToken)
    {
        var checks = new IDiagnosticCheck[]
        {
            new ConfigDiagnosticCheck(),
            new ApiKeyDiagnosticCheck(_config),
            new ConnectionDiagnosticCheck(_httpClientFactory)
        };

        var report = await _runner.RunAsync(checks, cancellationToken);

        var output = options.Json
            ? JsonReporter.Format(report)
            : new TextDiagnosticReportFormatter().Format(report);

        console.WriteLine(output);
        return report.IsSuccess ? 0 : 1;
    }
}
```

All real work happens here. Dependencies are constructor-injected. No `System.CommandLine` types.

### DI Bridge Middleware

Microsoft DI services must be explicitly forwarded into `System.CommandLine`'s invocation `BindingContext`. This is done via a middleware that runs before each command handler:

```csharp
public static CommandLineBuilder UseDependencyInjection(
    this CommandLineBuilder builder,
    IServiceProvider serviceProvider)
{
    return builder.AddMiddleware(async (context, next) =>
    {
        context.BindingContext.AddService<IServiceProvider>(_ => serviceProvider);

        foreach (var service in GetAllServiceTypes(serviceProvider))
        {
            context.BindingContext.AddService(
                service,
                sp => sp.GetRequiredService(service));
        }

        await next(context);
    });
}
```

This creates a per-invocation bridge so scoped and singleton services are correctly resolved.

### Startup

```csharp
var services = new ServiceCollection();
services.AddHttpClient(StudywiseDefaults.ApiName, client => { ... });
services.AddSingleton(config);
services.AddSingleton<DiagnosticRunner>();
services.AddSingleton<DoctorCommand>();
services.AddTransient<ICommandHandler<DoctorCommandOptions>, DoctorCommandHandler>();
// ... more registrations

var serviceProvider = services.BuildServiceProvider();

var rootCommand = new RootCommand();
foreach (var command in serviceProvider.GetServices<Command>())
{
    rootCommand.AddCommand(command);
}

var parser = new CommandLineBuilder(rootCommand)
    .UseDefaults()
    .UseDependencyInjection(serviceProvider)
    .Build();

return await parser.InvokeAsync(args);
```

### Testing

**Handler unit test** — construct the handler directly with fake/stub dependencies:

```csharp
var handler = new DoctorCommandHandler(fakeRunner, fakeHttpClientFactory, config);
var exitCode = await handler.HandleAsync(
    new DoctorCommandOptions(Json: true),
    testConsole,
    CancellationToken.None);
Assert.Equal(0, exitCode);
```

No `System.CommandLine`, no parser, no real diagnostics.

**Command wiring unit test** — register a fake handler, invoke the command:

```csharp
services.AddSingleton<ICommandHandler<DoctorCommandOptions>>(fakeHandler);
var command = serviceProvider.GetRequiredService<DoctorCommand>();
var root = new RootCommand { command };
var parser = new CommandLineBuilder(root)
    .UseDefaults()
    .UseDependencyInjection(serviceProvider)
    .Build();
await parser.InvokeAsync("doctor --json", testConsole);

// assert fakeHandler received correct options
```

This tests that CLI parsing produces the right `CommandOptions` without running diagnostics.

## Why This Pattern

- **Separation**: CLI parsing and business logic are in different classes, different namespaces, different concerns.
- **Testability**: Commands can be tested with fake handlers. Handlers can be tested with fake dependencies. No full CLI invocation needed for either.
- **Consistency**: `ICommandHandler<TOptions>` gives every command the same shape, making the codebase uniform and easier to navigate.
- **Scalability**: New commands follow the same three-class pattern. No per-command interface needed.
- **Honest DI**: Dependencies flow through constructors normally. No static factories, no service locator hidden inside command classes.
- **Minimal ceremony**: No source generators, no hosting package, no Spectre.Console migration. Just the bridge middleware and the interface.

## Alternatives Considered

- **Albatross.CommandLine**: adds source generation and a full command framework. Too much investment for the current scale.
- **Spectre.Console**: excellent DI support but implies replacing `System.CommandLine`. Not warranted for a CLI with 1 command.
- **Service location without middleware**: `context.BindingContext.GetRequiredService<T>()` would require manually adding every service to the pseudo-provider, which does not scale and does not support scoped services.
- **Static factory commands with DI**: still forces business logic into command classes or requires awkward service location patterns.

## When to Introduce New Interfaces

- `ICommandHandler<TOptions>` is already shared across all commands. Do not create per-command interfaces like `IDoctorCommandHandler`.
- If a command needs a specialized abstraction beyond `ICommandHandler<TOptions>`, introduce it only when the need is concrete, not preemptively.
- If testing requires a seam that the current pattern does not provide, revisit. Otherwise keep it simple.