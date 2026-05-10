# Issue #4 Plan — CLI Diagnostics Foundation (US-001)

## Summary
Implement the `studywise doctor` command foundation in the CLI so users can run diagnostics without arguments and get a complete report in either:
- human-readable text (default), or
- machine-readable JSON (`--json`)

This is **Track A (CLI-only)** and is the base for dependent diagnostics user stories (US-002..US-005).

---

## User Story
Som användare  
Vill jag köra `studywise doctor` utan argument och få en komplett diagnostikrapport  
Så att jag snabbt ser om CLI:n är korrekt konfigurerad

---

## Scope (This Issue)
- Add `doctor` command with `--json` boolean flag
- Run **three checks sequentially**:
  1. config
  2. api-key
  3. connection
- Produce readable default text output
- Produce JSON output when `--json` is provided
- Return exit code:
  - `0` when all checks pass
  - `1` when any check fails

---

## Out of Scope (Handled in later issues)
- Deep/production-grade validation logic for each check
- Advanced connection retry/backoff behavior
- Rich diagnostics remediation workflows
- Additional output formats beyond text + JSON

---

## CLI Contract

### Command
- `studywise doctor`
- `studywise doctor --json`
- `studywise doctor --help`

### Flags
- `--json` (boolean): output diagnostics report as JSON

### Output Modes
- Default: text
- JSON: only via `--json`
- No `--output` flag in this issue

### Decision: output flags
The original issue text mentions `--output`, but planning decision is to keep US-001 simple: text is the default and `--json` is the only output-mode flag. A generic `--output <format>` option is intentionally out of scope unless future CLI requirements justify multiple output formats beyond text and JSON.

---

## Required Text Output Format

### Pass example
```text
Studywise CLI Diagnostics

[PASS] Config: OK — /home/user/.config/studywise/config.json
[PASS] API-nyckel: OK — finns (maskerad)
[PASS] Connection: OK — /health svarar

All checks passed (3/3)
```

### Fail example
```text
Studywise CLI Diagnostics

[PASS]  Config: OK
[FAIL]  API-nyckel: FAIL — saknas i config
[WARN]  Connection: WARN — /health svarade med 503

1 failed, 1 passed, 1 warning (exit code 1)
```

Notes:
- Keep headings and markers stable (`[PASS]`, `[FAIL]`, `[WARN]`)
- Keep final summary line explicit and user-readable

---

## Architecture & Design

### 1) Command layer
Add `DoctorCommand` under existing command pattern:
- static `Create()` method
- registers `--json`
- invokes diagnostics orchestrator
- writes formatted output
- sets command exit code based on report status

### 2) Diagnostics core (new folder/module)
Create a diagnostics abstraction to support this and future stories:
- `IDiagnosticCheck`
  - `Name`
  - async `RunAsync(...)`
- `DiagnosticCheckResult`
  - check name
  - status (`Pass`, `Fail`, `Warn`)
  - message
  - optional details/meta
- `DiagnosticReport`
  - timestamp
  - ordered check results
  - aggregate counts (pass/fail/warn)
  - overall success/failure flag

### 3) Check implementations (foundation)
Implement three concrete checks:
- `ConfigDiagnosticCheck`
- `ApiKeyDiagnosticCheck`
- `ConnectionDiagnosticCheck`

For US-001 they establish the diagnostic contract and orchestration flow. They must execute in real sequence and return structured results, but detailed production validation belongs to later stories:
- US-002: config file existence/readability
- US-003: API-key existence/non-empty checks
- US-004: health endpoint connection behavior

### 4) Orchestrator
Add a diagnostics runner/service that:
- accepts list of checks
- runs them **in sequence** (no parallelization)
- aggregates results and summary counts
- returns `DiagnosticReport`

### 5) Output formatters
Add formatter separation:
- `TextDiagnosticReportFormatter`
- `JsonDiagnosticReportFormatter`

`DoctorCommand` selects formatter by `--json`.

---

## Proposed File Plan

### CLI project (`src/Studywise.Cli/`)
- `Commands/DoctorCommand.cs` (new)
- `Program.cs` (update: register doctor command)
- `Diagnostics/IDiagnosticCheck.cs` (new)
- `Diagnostics/DiagnosticStatus.cs` (new)
- `Diagnostics/DiagnosticCheckResult.cs` (new)
- `Diagnostics/DiagnosticReport.cs` (new)
- `Diagnostics/DiagnosticRunner.cs` (new)
- `Diagnostics/Checks/ConfigDiagnosticCheck.cs` (new)
- `Diagnostics/Checks/ApiKeyDiagnosticCheck.cs` (new)
- `Diagnostics/Checks/ConnectionDiagnosticCheck.cs` (new)
- `Diagnostics/Formatting/TextDiagnosticReportFormatter.cs` (new)
- `Diagnostics/Formatting/JsonDiagnosticReportFormatter.cs` (new)

### Tests
- Unit tests project:
  - `DoctorCommandTests.cs`
  - `DiagnosticRunnerTests.cs`
  - `TextDiagnosticReportFormatterTests.cs`
  - `JsonDiagnosticReportFormatterTests.cs`
- Integration tests project:
  - `DoctorCommandIntegrationTests.cs`
- E2E tests project:
  - `DoctorCommandE2ETests.cs`

---

## Test Plan

### Unit tests
1. `doctor` without flags uses text formatter
2. `doctor --json` uses JSON formatter
3. Checks execute in strict order: config -> api-key -> connection
4. Exit code is `0` when all checks pass
5. Exit code is `1` when any check fails
6. Text formatter outputs title, per-check status marker, summary line
7. JSON formatter outputs valid JSON with expected fields and statuses

### Integration tests
1. Invoke root command with `doctor` and verify text mode semantics
2. Invoke root command with `doctor --json` and verify JSON payload
3. Verify exit code behavior for pass/fail scenarios

### E2E tests (CLI process + Dev Proxy)
Use the existing `Studywise.CLI.E2ETests` pattern from `docs/testing/testing-strategy.md`: spawn the CLI as a separate process and mock HTTP through Microsoft Dev Proxy.

1. `studywise doctor` (default text mode)
   - Start Dev Proxy with deterministic mocks for diagnostics endpoints (including `/health`)
   - Run CLI process with `doctor`
   - Verify stdout contains `Studywise CLI Diagnostics`
   - Verify stdout contains one line each for `Config`, `API-nyckel`, and `Connection` with status markers
   - Verify summary line format (`All checks passed (3/3)` or failed/warn summary)
   - Verify process exit code matches report outcome (`0` all-pass, `1` if any fail)

2. `studywise doctor --json` (JSON mode)
   - Start Dev Proxy with deterministic mocks for the same scenario
   - Run CLI process with `doctor --json`
   - Verify stdout is valid JSON
   - Verify JSON includes ordered check results (`config`, `api-key`, `connection`) and statuses
   - Verify aggregate fields/counts and overall result are present
   - Verify process exit code follows same policy (`0` all-pass, `1` if any fail)

---

## Implementation Steps

1. Add diagnostics domain models and `IDiagnosticCheck`
2. Add `DiagnosticRunner` with sequential execution
3. Implement the three check classes (foundation behavior)
4. Implement text and JSON formatters
5. Implement `DoctorCommand` (`--json`, run diagnostics, print, set exit code)
6. Register doctor command in `Program.cs`
7. Add/adjust unit tests
8. Add integration tests
9. Add one E2E test class in `Studywise.CLI.E2ETests` covering text and JSON doctor invocation (Dev Proxy pattern)
10. Run: `dotnet build` + `dotnet test` (+ E2E test command if separated in CI/local)

---

## Manual Verification

After automated tests pass, verify the command manually:

```bash
dotnet run --project src/Studywise.Cli -- doctor
```

Expected: text output includes `Studywise CLI Diagnostics`, one line per check with `[PASS]`/`[FAIL]`/`[WARN]`, and a summary line.

```bash
dotnet run --project src/Studywise.Cli -- doctor --json
```

Expected: output is valid JSON containing the diagnostics report, ordered check results, statuses, and aggregate success/failure state.

---

## Acceptance Criteria Mapping

- `studywise doctor` runs all three checks in sequence → `DiagnosticRunner` + sequence test
- Output readable (text or JSON depending on flag) → formatters
- Exit code `0` if all pass, non-zero if any fail → command exit policy
- `--help` documents command → command description
- `--json` flag for machine-readable output → boolean option only
- Default text output readable for CLI users → text formatter structure + tests
