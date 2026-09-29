# Studywise CLI Architecture

## Status
**Datum:** 2026-09-29 (updated after NUnit + E2E-removal work)
**Typ:** Arkitekturdokument
**Projekt:** Studywise CLI

---

## 1. Solution Structure

```
studywise-cli/
├── Studywise.Cli.sln
├── src/
│   └── Studywise.Cli/                      # CLI application (System.CommandLine)
└── test/
    ├── Studywise.CLI.UnitTests/            # Unit tests (NUnit)
    └── Studywise.CLI.IntegrationTests/     # Integration tests (NUnit + WireMock)
```

> **Note:** A `Studywise.CLI.E2ETests` project existed historically and was
> removed in commit `9a2c5a6` (issue #40). Per the YHS org test strategy,
> E2E belongs in the frontend repo (Playwright). A CLI binary does not
> have E2E in the org sense.

> **Naming convention:** `Studywise.CLI.{TestType}` — product name + test type. No double "Tests".

---

## 2. Command Pattern

CLI commands follow a specific pattern for consistency and testability.

### Structure

```csharp
[AutoRegisterCommand]  // Enables auto-discovery via reflection
public sealed class MyCommand
{
    public static Command Create()
    {
        var command = new Command("mycommand", "Description of the command");
        
        // Add options
        var verboseOption = new Option<bool>("--verbose", "Enable verbose output");
        command.AddOption(verboseOption);
        
        // Set handler with DI
        command.SetHandler(async context =>
        {
            // Inject dependencies via BindingContext
            var httpClientFactory = context.BindingContext
                .GetRequiredService<IHttpClientFactory>();
            var httpClient = httpClientFactory.CreateClient("Studywise");
            
            // Command logic...
            
            context.ExitCode = 0;
        });
        
        return command;
    }
}
```

### Key Principles

| Principle | Rationale |
|-----------|-----------|
| **`[AutoRegisterCommand]`** | Auto-discovers commands at startup — no manual registration needed |
| **`static Create()`** | Commands are stateless; no instance needed. Static factory follows CLI convention |
| **No interface (`ICommandRegistration`)** | Unnecessary indirection. Attribute + static method is sufficient |
| **DI via `BindingContext`** | System.CommandLine provides proper DI integration. Use `context.BindingContext.GetRequiredService<T>()` |
| **Integration tests detect missing DI registrations** | A `DoctorCommand` registered in `Program.cs` but with no `ICommandHandler<DoctorCommandOptions>` would throw at handler-resolution time — the existing `DoctorCommandHandlerTests` fixture catches this |

### Dependency Injection Pattern

**Do this:**
```csharp
command.SetHandler(async context =>
{
    var httpClientFactory = context.BindingContext
        .GetRequiredService<IHttpClientFactory>();
    var httpClient = httpClientFactory.CreateClient("Studywise");
    
    // Use httpClient...
});
```

**Never this (anti-pattern):**
```csharp
// Static service locator — hard to test, hidden dependencies
var httpClient = CommandServices.GetHttpClient();
```

### Note on `AutoRegisterCommandAttribute`

The `[AutoRegisterCommand]` attribute exists in the codebase but is
**not currently used** — `Program.cs` does not scan the assembly for
types with this attribute; it registers commands explicitly via the
DI container:

```csharp
services.AddSingleton<Command, DoctorCommand>();
services.AddTransient<ICommandHandler<DoctorCommandOptions>, DoctorCommandHandler>();
```

The attribute is preserved as an extension point. If a future iteration
implements reflection-based auto-discovery, the test layer should cover
it with a fixture that registers a couple of dummy commands and asserts
they appear in `--help`.

---

## 3. Test Layers: Unit vs Integration

The CLI repo has two test layers. There is no E2E layer for a CLI
binary (see "Solution Structure" above for the historical note).

### IntegrationTests

**What it is:** Command handlers run in the test process with **mocked HTTP** (via WireMock.Net embedded).

**What it tests:**
- Handler argument parsing
- Correct HTTP response → CLI output mapping
- Error handling (404, 500, timeout)
- That the right command calls the right endpoint

**Runs:** With `dotnet test` in the same process as the test project — **no separate process spawned**.

**Example:**
```csharp
[Fact]
public async Task ListEducationLevels_ReturnsFormattedTable()
{
    // Arrange: mocked HTTP response
    var handler = new ListEducationLevelsHandler(
        new MockHttpClient(jsonEducationLevels));
    
    // Act
    var result = await handler.HandleAsync(new ListEducationLevelsCommand());
    
    // Assert
    Assert.NotNull(result);
    Assert.Contains("Name", result.Output);
}
```

### Future: end-to-end CLI behavior

The pre-2026-09 `Studywise.CLI.E2ETests` project did spawn the CLI as a
separate process to verify `--help` text and exit codes. That layer
was removed because per the YHS org test strategy, E2E belongs in
the frontend repo (Playwright). For a CLI binary, the equivalent
end-to-end coverage would be:

- A future Playwright suite against a CLI-in-a-browser context
  (e.g. via xterm.js + WebSocket bridge)
- Or an `execute()`-style smoke test in CI that runs the compiled
  binary with `--help` and asserts the exit code

Neither is in scope today. The integration tests cover the handler
logic end-to-end; the production `Program.cs` wiring is small enough
that the missing E2E layer has low cost.

---

## 4. Decisions Made

| Question | Status | Comment |
|----------|--------|---------|
| Separate projects? | ✅ **Yes** | UnitTests, IntegrationTests — one per layer |
| Naming convention? | ✅ **`Studywise.CLI.{TestType}`** | Product name + test type, no double Tests |
| E2E project for the CLI? | ❌ **No** | Per org strategy, E2E belongs in the frontend repo |
| Test framework? | ✅ **NUnit 5 + FluentAssertions 7** | Matches Studywise-Api / SparkProgress. Latest stable commercial-friendly FluentAssertions (8.x is paid for commercial use). |
| Mocking framework? | ✅ **Moq 4.21** | Same as Studywise-Api / SparkProgress |
| Command pattern? | ✅ **DI-registered `Command` subclass + `ICommandHandler<TOptions>`** | Not the auto-discovery flow documented in earlier revisions of this file |
| DI approach? | ✅ **`BindingContext.GetRequiredService<T>()`** | Proper DI, testable, no static locators |

---

_Created 2026-05-09, updated 2026-09-29 with NUnit migration + E2E removal._