# Issue #26 Plan: CLI Education Level List

## Purpose
Implement `studywise education-levels list --country <iso>` so operators can list education levels for a specific country using the existing API endpoint — without exposing an unfiltered/global list.

## Scope
**CLI only — no API changes.**

## Files To Modify/Create

### New files
| File | Purpose |
|------|---------|
| `src/Studywise.Cli/Commands/EducationLevels/EducationLevelsCommand.cs` | Command definition |
| `src/Studywise.Cli/Commands/EducationLevels/EducationLevelsCommandOptions.cs` | Options record |
| `src/Studywise.Cli/Commands/EducationLevels/EducationLevelsCommandHandler.cs` | Handler (HTTP call + output) |
| `src/Studywise.Cli/Commands/EducationLevels/EducationLevelResponse.cs` | API response model |
| `src/Studywise.Cli/Commands/EducationLevels/EducationLevelOutputFormatter.cs` | Text output formatter |
| `test/Studywise.CLI.UnitTests/Studywise.Cli.UnitTests/EducationLevelsCommandHandlerTests.cs` | Unit tests |

### Modified files
| File | Change |
|------|--------|
| `src/Studywise.Cli/Program.cs` | Register command + handler in DI |
| `test/Studywise.CLI.UnitTests/Studywise.Cli.UnitTests/Studywise.CLI.UnitTests.csproj` | Add test file |

## Functional Requirements

### Command structure
```
studywise education-levels list --country <iso>
```

- `--country` (required, string): ISO 3166-1 alpha-2/3 country code (e.g. `SWE`, `swe`)
- `--json` (optional, bool): Output raw JSON from API

### API call
- Endpoint: `GET /api/v1/education-levels?countryCode={iso}`
- Uses existing `IHttpClientFactory` named client (`StudywiseDefaults.ApiName`)
- Requires `STUDYWISE_API_KEY` (401 if missing)
- Country code is sent as-is (API validates)

### Output format

**Text (default)**:
```
Education Levels for SE
-----------------------
ID          Name                    Level   Has Grades
----------  ----------------------  -----   ---------
preschool   Förskola               1       No
primary     Grundskola              2       Yes
secondary   Gymnasieskola           3       Yes
```

> **Note:** `IsLocked` appears on the outer envelope (`EducationLevelListResult.IsLocked`), not on individual items.

**JSON (`--json`)**:
Raw API JSON envelope, e.g.:
```json
{ "countryCode": "SE", "isLocked": false, "educationLevels": [
  { "id": "preschool", "name": "Förskola", "hasGrades": false, "organizingLevel": 1, "countryCode": "SE" },
  ...
]}
```

## API Response Model

The API returns an **envelope** (`EducationLevelListResult`), not a bare array:

```csharp
// Envelope (what the API actually returns)
public sealed record EducationLevelListResult(
    string CountryCode,
    bool IsLocked,
    IEnumerable<EducationLevelResult> EducationLevels
);

// Per-item (what's inside the envelope)
public sealed record EducationLevelResult(
    string Id,
    string Name,
    bool HasGrades,
    int OrganizingLevel,
    string CountryCode,
    AIReviewResult AiReview
);
```

> **AI Review note:** The CLI does not need to display the `AiReview` field in text output. It is included in JSON output via `JsonSerializerOptions.WriteIndented`.

## Acceptance Criteria (Given/When/Then)

### AC1: Missing --country flag
- **Given** no `--country` argument
- **When** user runs `studywise education-levels list`
- **Then** CLI prints `error: --country is required` to stderr
- **And** exits with code 1

### AC2: Valid country with auth
- **Given** valid `STUDYWISE_API_KEY` and `--country SWE`
- **When** user runs `studywise education-levels list --country SWE`
- **Then** CLI calls `GET /api/v1/education-levels?countryCode=SWE`
- **And** exits with code 0
- **And** prints education levels in text table format

### AC3: JSON output
- **Given** valid auth and `--country SWE --json`
- **When** user runs `studywise education-levels list --country SWE --json`
- **Then** CLI prints the raw API JSON envelope to stdout
- **And** exits with code 0

### AC4: API 401
- **Given** missing or invalid `STUDYWISE_API_KEY`
- **When** user runs `studywise education-levels list --country SWE`
- **Then** CLI prints `error: unauthorized — check STUDYWISE_API_KEY` to stderr
- **And** exits with code 1

### AC5: API error
- **Given** valid auth but API returns 4xx/5xx
- **When** user runs `studywise education-levels list --country SWE`
- **Then** CLI prints `error: failed to fetch education levels ({statusCode})` to stderr
- **And** exits with code 1

### AC6: Network error
- **Given** valid auth but API unreachable
- **When** user runs `studywise education-levels list --country SWE`
- **Then** CLI prints `error: could not reach API ({exception message})` to stderr
- **And** exits with code 1

## Explicit Boundaries
- Do not add `--all` or any flag that returns education levels without a country filter
- Do not cache education levels
- Do not add pagination (API handles this)
- Do not add a `studywise education-levels` parent command without subcommands (simple flat structure)
- Do not change existing `doctor` command or diagnostics framework
- Do not display `AiReview` fields in text output (only in JSON)

## Verification
```bash
# Build
dotnet build

# Unit tests (once implemented)
dotnet test --filter "FullyQualifiedName~EducationLevels"

# Manual (requires running API)
studywise education-levels list --country SWE
studywise education-levels list --country SWE --json
studywise education-levels list  # should error with --country required
```

## Risks and Mitigations
- **Risk**: API response schema differs from expected fields.
  - **Mitigation**: Use `JsonSerializer.Deserialize<EducationLevelListResult>` with source-generated or explicit properties; handle missing fields with defaults.
- **Risk**: Country code format (uppercase vs lowercase).
  - **Mitigation**: Send as-is; API validates. If API requires uppercase, document it.
- **Risk**: Empty results for valid country.
  - **Mitigation**: Print `No education levels found for {countryCode}` with exit code 0.

## Plan Revisions
- *Revision 1 (2026-06-17):* AI Review (M3) identified two issues: (1) detailed plan was untracked local file, not committed to branch — fixed by committing; (2) `EducationLevelResponse` model had wrong fields (`Order`, `IsLocked` per-item) — fixed to match actual `EducationLevelResult.cs` with `OrganizingLevel` and envelope structure.