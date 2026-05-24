# Feature: `--check` Flag for Selective Doctor Checks

**Issue:** [#8](https://github.com/yellow-house-studio/studywise-cli/issues/8)  
**PR:** [#19](https://github.com/yellow-house-studio/studywise-cli/pull/19)  
**Date:** 2026-05-22  
**Status:** Implemented  

## Traceability
- Issue: https://github.com/yellow-house-studio/studywise-cli/issues/8
- PR: https://github.com/yellow-house-studio/studywise-cli/pull/19
- Plan: `docs/plans/08-check-flag.md`

---

## Overview

The `studywise doctor` command now accepts a `--check` option to run a specific diagnostic check instead of all checks. This enables targeted troubleshooting.

## Usage

```bash
# Run all checks (default)
studywise doctor

# Run only config check
studywise doctor --check config

# Run only API key check
studywise doctor --check api-key

# Run only connection check
studywise doctor --check connection

# Explicitly run all checks
studywise doctor --check all

# Case-insensitive
studywise doctor --check CONFIG
studywise doctor --check Api-Key

# Unknown check
studywise doctor --check foo
# Output: unknown check: foo (to stderr)
# Exit code: 1
```

## Available Checks

| Check Name | What It Validates |
|------------|-------------------|
| `config` | CLI configuration file exists and is valid |
| `api-key` | API key is set and valid |
| `connection` | Can reach the Studywise API endpoint |
| `all` | Runs all three checks in order (default) |

## Exit Codes

| Scenario | Exit Code |
|----------|-----------|
| Check passes | 0 |
| Check fails | 1 |
| Unknown check name | 1 (with error message) |

## Architecture

### Files Modified

- `src/Studywise.Cli/Commands/Doctor/DoctorCommandOptions.cs` — Added `CheckName` property
- `src/Studywise.Cli/Commands/Doctor/DoctorCommand.cs` — Added `--check` option with `SetHandler` binding
- `src/Studywise.Cli/Commands/Doctor/DoctorCommandHandler.cs` — Added check filtering logic

### Key Implementation Details

**Option binding (critical pattern):**
```csharp
var checkOption = new Option<string>(
    name: "--check",
    description: "Which check to run: config, api-key, connection, or all (default)",
    getDefaultValue: () => "all");
AddOption(checkOption);

SetHandler(async (context, cancellationToken) =>
{
    var checkName = context.ParseResult.GetValueForOption(checkOption);
    var options = new DoctorCommandOptions(json, checkName);
    await HandleAsync(options, console, cancellationToken);
}, jsonOption, checkOption);  // ← Both options must be passed
```

**Check resolution:**
```csharp
IDiagnosticCheck[]? ResolveChecks(string checkName)
{
    return checkName.ToLowerInvariant() switch
    {
        "all" => new[] { new ConfigCheck(), new ApiKeyCheck(), new ConnectionCheck(httpClientFactory) },
        "config" => new[] { new ConfigCheck() },
        "api-key" => new[] { new ApiKeyCheck() },
        "connection" => new[] { new ConnectionCheck(httpClientFactory) },
        _ => null  // null → error message + exit 1
    };
}
```

## Testing

```bash
# Run all tests
dotnet test

# Run doctor-specific tests
dotnet test --filter "FullyQualifiedName~Doctor"

# Manual verification
dotnet run --project src/Studywise.Cli -- doctor --check config
dotnet run --project src/Studywise.Cli -- doctor --check unknown-check
```

## Related Documentation

- Feature Plan: `docs/plans/08-check-flag.md`
- After-Action Report: `docs/after-action-report/check-flag.md`
- CLI Integration Tests: `tests/Studywise.Cli.IntegrationTests/`