# Issue #8 Plan: `--check` Flag for `studywise doctor`

## Purpose
Allow users to run only a specific diagnostic check via `studywise doctor --check <name>`, enabling targeted troubleshooting without executing all checks.

## Scope
- In scope:
  - New `--check` CLI option on `doctor` command
  - Filtering logic in `DoctorCommandHandler` to run a subset of checks
  - Validation for unknown check names with a clear error message
  - `--check all` as default (runs all checks in order)
- Out of scope:
  - Changes to existing check implementations
  - New diagnostic checks
  - Changes to output format or exit code behavior

## Dependencies
- Depends on US-001 (foundation, already implemented)
- No additional external dependencies

## Files To Modify
- `src/Studywise.Cli/Commands/Doctor/DoctorCommandOptions.cs` — add `CheckName` property
- `src/Studywise.Cli/Commands/Doctor/DoctorCommand.cs` — add `--check` option
- `src/Studywise.Cli/Commands/Doctor/DoctorCommandHandler.cs` — filter checks by name

## Functional Requirements (WHAT)

### 1) `--check` option
- `studywise doctor --check config` runs only the `config` check
- `studywise doctor --check api-key` runs only the `api-key` check
- `studywise doctor --check connection` runs only the `connection` check
- `studywise doctor --check all` (default) runs all checks in order: `config`, `api-key`, `connection`
- Unknown check name: output `unknown check: <name>` to stderr and exit with code 1

### 2) Check name resolution
- Check names are case-sensitive
- Valid check names: `config`, `api-key`, `connection`, `all`
- The option accepts a single string value

### 3) Exit code
- Valid check (known name): exit 0 if PASS, exit 1 if FAIL
- Unknown check name: exit 1 immediately (no diagnostic run attempted)

## Code Design (HOW)

### Option Model
```csharp
// DoctorCommandOptions.cs
public sealed record DoctorCommandOptions(bool Json, string CheckName = "all");
```

### CLI Option
```csharp
// DoctorCommand.cs — add to the command:
var checkOption = new Option<string>(
    name: "--check",
    description: "Which check to run: config, api-key, connection, or all (default)",
    getDefaultValue: () => "all");
AddOption(checkOption);
```

### Handler Filtering
```csharp
// DoctorCommandHandler.cs — in HandleAsync:
var allChecks = new IDiagnosticCheck[]
{
    new ConfigDiagnosticCheck(),
    new ApiKeyDiagnosticCheck(),
    new ApiKeyDiagnosticCheck(configPath),  // existing ctor
    new ConnectionDiagnosticCheck(_httpClientFactory)
};

// Resolve which checks to run
var checks = options.CheckName.ToLowerInvariant() switch
{
    "all" => allChecks,
    "config" => new[] { allChecks[0] },
    "api-key" => new[] { allChecks[1] },
    "connection" => new[] { allChecks[2] },
    _ => throw new InvalidOperationException($"unknown check: {options.CheckName}")
};
```

For unknown check names, write to stderr and return exit code 1 **before** running diagnostics:
```csharp
if (!new[] { "all", "config", "api-key", "connection" }.Contains(options.CheckName.ToLowerInvariant()))
{
    console.Error.WriteLine($"unknown check: {options.CheckName}");
    return 1;
}
```

### Verification
- Build: `dotnet build`
- Run unit tests: `dotnet test --filter "FullyQualifiedName~DoctorCommandHandlerTests"`
- Run all tests: `dotnet test`

## Acceptance Criteria (GIVEN/WHEN/THEN)

### AC1: `--check config` runs only config check
- Given no additional setup
- When user runs `studywise doctor --check config`
- Then output shows only the `config` check result
- And no `api-key` or `connection` check is executed

### AC2: `--check api-key` runs only api-key check
- Given no additional setup
- When user runs `studywise doctor --check api-key`
- Then output shows only the `api-key` check result

### AC3: `--check connection` runs only connection check
- Given no additional setup
- When user runs `studywise doctor --check connection`
- Then output shows only the `connection` check result

### AC4: `--check all` (default) runs all checks
- Given no additional setup
- When user runs `studywise doctor --check all` (or omits `--check`)
- Then all three checks run in order: `config`, `api-key`, `connection`

### AC5: Unknown check name produces clear error
- Given no additional setup
- When user runs `studywise doctor --check foo`
- Then `unknown check: foo` is written to stderr
- And exit code is 1
- And no check is executed

## Existing Patterns Used
- `ICommandHandler<T>` pattern — already used for `DoctorCommandHandler`
- `IDiagnosticCheck[]` array — already constructed in `HandleAsync`
- Option pattern with `getDefaultValue: () => "all"` — matches existing `--json` option style
- Exit code 1 for failure — already established in `DoctorCommandHandler`