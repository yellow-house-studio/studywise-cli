# Issue #6 Plan — CLI Diagnostics API-key Exists-check (US-003)

## Summary
Implement the API-key diagnostics behavior for `studywise doctor` so the `api-key` check validates whether a key exists in config and is non-empty, without validating key correctness against the backend.

This issue builds on the doctor foundation (US-001) and aligns output with the diagnostics contract.

---

## User Story
Som användare  
Vill jag att `studywise doctor` verifierar att en API-nyckel är konfigurerad  
Så att jag vet om nyckeln overhuvudtaget finns i config

---

## Scope (This Issue)
- Update `ApiKeyDiagnosticCheck` to check configured key presence
- Accept both config names: `apiKey` and `api_key`
- Require non-empty, non-whitespace string to PASS
- Return FAIL with explicit message when missing/empty
- Keep key redacted in all outputs

---

## Out of Scope
- Verifying whether key is valid/authorized against API
- Calling remote services from this check
- Introducing new output formats beyond existing text/JSON

---

## Acceptance Criteria Mapping

1. PASS when `apiKey` or `api_key` exists and is non-empty
   - `ApiKeyDiagnosticCheck` resolves key from config abstraction and treats whitespace-only values as empty.

2. FAIL with clear message when missing or empty
   - Fail message explicitly states key is missing/empty in config.

3. Do NOT validate key correctness
   - Check performs local config inspection only.

4. Never print key in cleartext
   - Messages continue using redacted wording only (for example `finns (maskerad)`), no interpolation of actual value.

---

## Design Notes

### Current gap
- `ApiKeyDiagnosticCheck` currently reads `ApplicationConfig.ApiKey` and reports missing key in environment variable.
- Issue #6 requires config-oriented messaging and alias support (`apiKey`/`api_key`).

### Proposed approach
- Keep check logic local and synchronous via existing `RunAsync` contract.
- Expand configuration model so key can be sourced from config aliases and normalized into one runtime property.
- Update diagnostics text to reference config (not environment variable).

### Config alias strategy
- Preferred: normalize once in config loading layer:
  - `apiKey` primary
  - fallback to `api_key`
- `ApiKeyDiagnosticCheck` reads normalized property only; no parsing logic inside check.

This keeps the check simple and reusable regardless of source (file/env overrides).

---

## Proposed File Changes

- `src/Studywise.Cli/Configuration/ApplicationConfig.cs`
  - Ensure config loading supports both `apiKey` and `api_key` aliases.
  - Normalize into `ApiKey` runtime field/property.

- `src/Studywise.Cli/Diagnostics/Checks/ApiKeyDiagnosticCheck.cs`
  - PASS when normalized key is non-empty/non-whitespace.
  - FAIL message updated to "saknas eller ar tom i config" (or equivalent clear phrasing).
  - Keep masked wording on PASS.

- `test/**` (existing diagnostics/config test projects)
  - Add/adjust tests for alias support, empty-string handling, and output message expectations.

---

## Test Plan

### Unit tests
1. `ApiKeyDiagnosticCheck` returns PASS for non-empty `ApiKey`.
2. `ApiKeyDiagnosticCheck` returns FAIL for `null`, empty, and whitespace values.
3. PASS message does not contain actual key value.
4. FAIL message references missing/empty config key.

### Configuration tests
1. Config containing `apiKey` maps to `ApplicationConfig.ApiKey`.
2. Config containing `api_key` maps to `ApplicationConfig.ApiKey`.
3. When both exist, precedence is deterministic (`apiKey` first).

### Integration test (doctor)
1. `studywise doctor` reports `[PASS] API-nyckel` when key exists.
2. `studywise doctor` reports `[FAIL] API-nyckel` when key is missing/empty.
3. Output never includes raw API-key value.

---

## Implementation Steps

1. Update config binding/normalization to support `apiKey` and `api_key`.
2. Adjust `ApiKeyDiagnosticCheck` to use normalized config key and revised messages.
3. Add/update unit tests for check behavior.
4. Add/update config mapping tests for alias handling.
5. Run `dotnet test` and verify diagnostics output snapshots/assertions.

---

## Risks and Mitigations

- Risk: Alias parsing logic duplicated across layers.
  - Mitigation: centralize alias resolution in `ApplicationConfig` loading.

- Risk: Accidental key leakage in logs/tests.
  - Mitigation: assert that output contains masked text and does not contain known sample key.

---

## Definition of Done

- All acceptance criteria for issue #6 are met.
- `ApiKeyDiagnosticCheck` behavior is covered by tests.
- `studywise doctor` output remains stable and key-safe in text and JSON modes.
