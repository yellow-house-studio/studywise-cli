# Issue #278 Plan — CLI: Education Level List

## Summary
Add a `studywise education-levels` command to the CLI that lists all available education levels by querying the existing API endpoint `GET /api/v1/education-levels?countryCode=SWE`.

---

## What
- Create a new `EducationLevelsCommand` (`education-levels` alias) that calls `GET /api/v1/education-levels?countryCode=SWE`.
- Default to `countryCode=SWE` — no new API endpoint needed.
- Support `--country-code <code>` flag for other countries.
- Display levels as a formatted text table (Name | Has Grades | Organizing Level).
- Require authentication (uses family binding via Auth0 token).
- Command lives in `src/Studywise.Cli/Commands/EducationLevels/`.

---

## Why
- Users (and agents acting as users) need to know which education levels exist in the system without remembering the country code.
- The API already supports listing levels; the CLI just needs a command to surface it.
- Defaulting to SWE aligns with the Swedish study buddy use case.

---

## Acceptance Criteria (Given/When/Then)

### AC1 — Lists education levels for default country (SWE)
Given the CLI is authenticated  
When the user runs `studywise education-levels`  
Then the CLI calls `GET /api/v1/education-levels?countryCode=SWE`  
And displays all returned levels in a table.

### AC2 — Lists education levels for a specified country
Given the CLI is authenticated  
When the user runs `studywise education-levels --country-code USA`  
Then the CLI calls `GET /api/v1/education-levels?countryCode=USA`  
And displays all returned levels in a table.

### AC3 — Handles unauthenticated request
Given the CLI is not authenticated  
When the user runs `studywise education-levels`  
Then the CLI returns an appropriate error (exit code non-zero).

### AC4 — Handles empty list
Given the CLI is authenticated but no levels exist for the country  
When the user runs `studywise education-levels --country-code XXX`  
Then the CLI displays an empty table or "No education levels found" message.

### AC5 — Handles API error
Given the CLI is authenticated  
When the API returns an error or is unreachable  
Then the CLI returns an error message with exit code non-zero.

---

## CLI Design

### Command
```
studywise education-levels [options]
studywise education-level [options]     # alias
```

### Arguments / Options
| Option | Type | Default | Description |
|--------|------|---------|-------------|
| `--country-code` | string | `SWE` | ISO 3-letter country code |
| `--json` | flag | false | Output raw JSON instead of table |

### Output Format (text)
```
Education Levels (SWE)
────────────────────────────────────────────────
Name              │ Has Grades │ Organizing Level
────────────────────────────────────────────────
Förskola          │ No         │ 0
Grundskola        │ Yes        │ 1
Gymnasium         │ Yes        │ 2
Universitet       │ No         │ 3
────────────────────────────────────────────────
```

### Output Format (JSON flag)
```json
{
  "countryCode": "SWE",
  "isLocked": false,
  "educationLevels": [
    { "id": "abc123", "name": "Förskola", "hasGrades": false, "organizingLevel": 0 },
    { "id": "def456", "name": "Grundskola", "hasGrades": true, "organizingLevel": 1 }
  ]
}
```

---

## API Contract
- **Endpoint:** `GET /api/v1/education-levels?countryCode={countryCode}`
- **Auth:** Required (Bearer token via Auth0)
- **Response:** `EducationLevelListResult`
  ```json
  {
    "countryCode": "SWE",
    "isLocked": false,
    "educationLevels": [
      { "id": "...", "name": "...", "hasGrades": true/false, "organizingLevel": 0/1/2/..., "countryCode": "...", "aiReview": {...} }
    ]
  }
  ```

---

## Project Structure
```
src/Studywise.Cli/
  Commands/
    EducationLevels/
      EducationLevelsCommand.cs       # Command definition
      EducationLevelsCommandHandler.cs # Handler (calls API, formats output)
      EducationLevelsCommandOptions.cs  # Options (country-code, json)
```

---

## Implementation Notes
- Reuse existing HTTP client factory (`IHttpClientFactory`) pattern from `ConnectionDiagnosticCheck`.
- Reuse `StudywiseDefaults.ApiName` for client naming.
- Reuse authentication pattern from other authenticated commands (check if a token is present — if not, fail with clear message).
- Use `HttpClient.GetAsync` with the bearer token in the `Authorization` header.
- Use the same table formatting style as other list commands in the CLI (check `doctor` output style or other existing formatting).
- Return exit code 0 on success, 1 on error.

---

## Boundaries
- In scope: command definition, API call, output formatting, error handling.
- Out of scope: creating, updating, or deleting education levels (API-only operations).

---

## Verification Steps

### Automated Tests
1. **Unit test:** `EducationLevelsCommandOptions` parses `--country-code` correctly.
2. **Unit test:** Handler calls correct endpoint for default and custom country codes.
3. **Unit test:** Handler formats JSON output correctly.
4. **Integration test:** CLI returns levels for `SWE` and `USA`.
5. **Integration test:** CLI returns error when not authenticated.
6. **Integration test:** CLI handles empty level list gracefully.

### Manual Verification
```bash
# Authenticate (set token)
export STUDYWISE_API_KEY="..."

# List levels for default (SWE)
dotnet run --project src/Studywise.Cli -- education-levels

# List levels for USA
dotnet run --project src/Studywise.Cli -- education-levels --country-code USA

# List as JSON
dotnet run --project src/Studywise.Cli -- education-levels --json

# cURL equivalent (for API verification)
curl -H "Authorization: Bearer <token>" \
  "https://api.studywiseapp.com/api/v1/education-levels?countryCode=SWE"
```

---

## Dependencies
- Existing `IHttpClientFactory` DI registration in `Program.cs`
- Existing auth token handling pattern
- `EducationLevelListResult` response model (API side — read-only reference)
- `StudywiseDefaults.ApiName` constant