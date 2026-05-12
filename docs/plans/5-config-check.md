# Issue #5 Plan — Config Check Enhancement (US-002)

## Summary
Enhance the existing `ConfigDiagnosticCheck` so `studywise doctor` treats config problems as hard failures when required by spec, validates readability in addition to existence, and resolves config path through environment override plus platform-standard defaults.

---

## What
- Expand `ConfigDiagnosticCheck` responsibility from existence-only validation to full config path + availability validation.
- Change missing config result from `WARN` to `FAIL`.
- Add explicit unreadable-config detection and return `FAIL` when the file exists but cannot be read.
- Enforce config path resolution precedence:
  1. `STUDYWISE_CONFIG` environment variable (when set)
  2. Platform default path when override is not set:
     - Linux/macOS (XDG-style): `~/.config/studywise/config.json`
     - Windows: `%APPDATA%\\studywise\\config.json`
- Ensure check output messages are explicit and include the resolved config path for each outcome (pass/missing/unreadable).

---

## Why
- A missing config file prevents correct CLI operation and should be a failure signal, not a warning.
- Existence alone is insufficient; unreadable config is operationally equivalent to missing config.
- Environment override support is required for CI, containers, and custom deployments.
- Platform-standard defaults reduce user confusion and align diagnostics with expected OS behavior.

---

## Acceptance Criteria (Given/When/Then)

### AC1 — PASS when config exists and is readable
Given a config file exists at the resolved config path  
When `studywise doctor` runs the config diagnostic check  
Then the check status is `PASS`  
And the message confirms config is accessible and includes the resolved path.

### AC2 — FAIL when config file is missing
Given no config file exists at the resolved config path  
When `studywise doctor` runs the config diagnostic check  
Then the check status is `FAIL`  
And the message clearly states the config file is missing and includes the expected path.

### AC3 — FAIL when config exists but is unreadable
Given a config file exists at the resolved path but cannot be read due to permissions/access  
When `studywise doctor` runs the config diagnostic check  
Then the check status is `FAIL`  
And the message clearly states the file is unreadable (permission/access issue) and includes the path.

### AC4 — Environment override is respected
Given `STUDYWISE_CONFIG` is set to a custom file path  
When `studywise doctor` runs the config diagnostic check  
Then that override path is used as the resolved config path  
And the result reflects the status of that override path.

### AC5 — Platform default path is used when override is absent
Given `STUDYWISE_CONFIG` is not set  
When `studywise doctor` runs on Linux/macOS  
Then the resolved default path is `~/.config/studywise/config.json`.

Given `STUDYWISE_CONFIG` is not set  
When `studywise doctor` runs on Windows  
Then the resolved default path is `%APPDATA%\\studywise\\config.json`.

---

## Verification Steps
- Validate readable config at default path returns `PASS` and includes resolved path.
- Validate missing config at default path returns `FAIL` (not `WARN`) and includes expected path.
- Validate unreadable config at default path returns `FAIL` and includes permission/readability reason.
- Validate `STUDYWISE_CONFIG` override with:
  - readable file -> `PASS`
  - missing file -> `FAIL`
  - unreadable file -> `FAIL`
- Validate reported path always matches the actual resolved path source (override vs default).
- Validate no regressions in other diagnostics checks (`api-key`, `connection`) and existing doctor exit-code policy.

---

## Boundaries
- In scope: config path resolution, existence validation, readability validation, and status/message correctness.
- Out of scope: config JSON schema/content validation.
- Out of scope: behavior changes in non-config checks (`ApiKeyDiagnosticCheck`, `ConnectionDiagnosticCheck`).
- Out of scope: broader diagnostics formatting redesign beyond message clarity needed for this check.

---

## Dependencies
- Existing diagnostics contracts and flow:
  - `IDiagnosticCheck`
  - `DiagnosticCheckResult`
  - `DiagnosticStatus`
  - `DiagnosticRunner`
  - `DoctorCommandHandler`
- Runtime environment variable availability (`STUDYWISE_CONFIG`).
- OS-specific user config directory behavior on Linux/macOS/Windows.
- Filesystem permission model for unreadable-file scenarios.
