# Studywise CLI Architecture

## Status
**Datum:** 2026-05-10
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
    ├── Studywise.CLI.UnitTests/            # Unit tests
    ├── Studywise.CLI.IntegrationTests/     # Integration tests (mocked HTTP)
    └── Studywise.CLI.E2ETests/             # E2E: spawn actual CLI process
```

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

### Auto-Registration Flow

1. `Program.cs` scans assembly for types with `[AutoRegisterCommand]`
2. For each type, calls `Create()` via reflection
3. Returns `Command` is added to root command
4. Service provider is available via `BindingContext` in handlers

```csharp
var commandTypes = assembly.GetTypes()
    .Where(t => t.IsClass 
                && !t.IsAbstract 
                && t.GetCustomAttributes(typeof(AutoRegisterCommandAttribute), false).Length > 0
                && t.GetMethod("Create") != null);

foreach (var type in commandTypes)
{
    var createMethod = type.GetMethod("Create");
    var command = createMethod?.Invoke(null, null) as Command;
    if (command != null)
    {
        rootCommand.AddCommand(command);
    }
}
```

---

## 3. Test Layers: Integration vs CLI E2E

The two final test layers (IntegrationTests and E2ETests) are related but fundamentally different:

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

### E2ETests (E2E — separate process)

**What it is:** Builds CLI and **spawns it as a separate process**. Verifies stdin/stdout/exit codes.

**What it tests:**
- CLI starts without crash
- `--help` shows correct usage
- Correct output format for list commands
- Exit codes for error handling
- Help text and command structure
- Argument parsing in "production" mode

**Runs:** `Process.Start()` in test — CLI is compiled and run as `dotnet run` or built binary.

**No HTTP** — this is pure smoke/sanity for CLI entry point.

**Example:**
```csharp
[Fact]
public void Help_Command_ExitsWithZero()
{
    // Arrange
    var cliPath = GetCliBinPath(); // bin/Debug/net10.0/studywise
    
    using var process = new Process { StartInfo = new ProcessStartInfo
    {
        FileName = cliPath,
        Arguments = "--help",
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        UseShellExecute = false
    }};
    
    // Act
    process.Start();
    process.WaitForExit();
    
    // Assert
    Assert.Equal(0, process.ExitCode);
    Assert.Contains("Usage:", process.StandardOutput.ReadToEnd());
}
```

### Comparison

| Aspect | IntegrationTests | E2ETests |
|--------|-----------------|----------|
| Process | Same process as test | **Separate CLI process** |
| HTTP | ✅ mocked | ❌ (no HTTP) |
| Speed | Fast | Slightly slower |
| What it verifies | Logic, parsing, mapping | **Actual CLI behavior** |
| Can crash CLI | No | Yes (shell verification) |

### Why separate projects?

- **Different dependencies:** IntegrationTests needs `HttpClient` mocks. E2ETests only needs `dotnet build` + process start.
- **Different environment requirements:** E2ETests needs CLI built first — IntegrationTests doesn't.
- **Different stability profile:** A crashed CLI process doesn't affect IntegrationTests.
- **Different test time:** E2ETests are slower (process spawn + build).

**Decision:** E2ETests is a **separate project** alongside IntegrationTests.

---

## 4. Decisions Made

| Question | Status | Comment |
|----------|--------|---------|
| Separate projects? | ✅ **Yes** | UnitTests, IntegrationTests, E2ETests — all own projects |
| Naming convention? | ✅ **`Studywise.CLI.{TestType}`** | Product name + test type, no double Tests |
| E2ETests need API mock? | **No** | Help/Exit-code tests don't need HTTP |
| Command pattern? | ✅ **Attribute + static Create()** | Auto-discovery, no interface needed |
| DI approach? | ✅ **`BindingContext.GetRequiredService<T>()`** | Proper DI, testable, no static locators |

---

_Created 2026-05-09, updated 2026-05-10 with command pattern documentation_