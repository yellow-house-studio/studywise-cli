# Issue #7 Plan: CLI Diagnostics US-004 Connection Check

## Purpose
Ensure `studywise doctor` verifies backend reachability by calling the configured API health endpoint and reporting deterministic PASS/FAIL outcomes that are actionable for users.

## Scope
- In scope:
  - Connection check result semantics for HTTP and transport outcomes.
  - Redirect boundary for the named HTTP client used by diagnostics.
  - Unit test updates for connection-check behavior.
- Out of scope:
  - Replacing the diagnostics framework or status model.
  - New CLI commands or output formats.
  - Auth or API-key behavior changes.

## Dependencies
- Depends on US-003 for base URL sourcing consistency.
- This plan assumes `ApplicationConfig.ApiBaseUrl` is the single source of truth consumed by diagnostics.

## Files To Modify
- `src/Studywise.Cli/Diagnostics/Checks/ConnectionDiagnosticCheck.cs`
- `src/Studywise.Cli/Program.cs`
- `test/Studywise.CLI.UnitTests/Studywise.Cli.UnitTests/ConnectionDiagnosticCheckBoundaryTests.cs`

## Functional Requirements (WHAT)

### 1) Health probe behavior
- The connection check calls HTTP GET on `{configured api base url}/health`.
- The check uses a 5 second timeout for the request.

### 2) Status semantics
- PASS only when response is HTTP 2xx.
- FAIL when response is HTTP 4xx/5xx, including status code in message.
- FAIL when endpoint is unreachable (DNS, network, socket, timeout).

### 3) Redirect boundary
- The diagnostics HTTP client follows at most one redirect.
- Responses beyond that redirect budget are treated as non-success and reported as FAIL by check semantics.

### 4) Cancellation boundary
- Distinguish timeout/unreachable failures from explicit user cancellation.
- Explicit user cancellation (for example Ctrl+C) is treated as an aborted run, not a health-check failure.
- Cancellation must not be reclassified as WARN/FAIL inside the connection check.

### 5) Output compatibility
- Works consistently for both text output (`studywise doctor`) and JSON output (`studywise doctor --json`).
- Existing check naming (`connection`) remains stable.

### 6) Exit code behavior
- `studywise doctor` exits non-zero when any check is FAIL.
- WARN behavior remains unchanged at framework level (WARN does not by itself force non-zero), but connection-check scenarios in this issue map to FAIL.

## Acceptance Criteria (Given/When/Then)

### AC1: PASS on healthy endpoint
- Given a configured API base URL whose `/health` endpoint returns 200
- When `studywise doctor` runs
- Then the `connection` check status is PASS
- And the message indicates successful `/health` response

### AC2: FAIL on HTTP client/server error statuses
- Given a configured API base URL whose `/health` endpoint returns 404 or 503
- When `studywise doctor` runs
- Then the `connection` check status is FAIL
- And the message includes the HTTP status code

### AC3: FAIL on timeout
- Given a configured API base URL where `/health` does not complete within 5 seconds
- When `studywise doctor` runs
- Then the `connection` check status is FAIL
- And the message clearly indicates timeout after 5 seconds

### AC4: FAIL on unreachable host
- Given a configured API base URL that cannot be reached (DNS/network/socket failure)
- When `studywise doctor` runs
- Then the `connection` check status is FAIL
- And the message clearly indicates reachability failure

### AC5: User-initiated cancellation is abort, not FAIL
- Given `studywise doctor` is running and the user cancels execution (Ctrl+C)
- When cancellation reaches the connection check
- Then the check does not convert cancellation into WARN or FAIL
- And the doctor run is aborted by cancellation semantics
- And no misleading health-failure message is produced for that cancellation path

### AC6: Redirect cap enforced
- Given `/health` causes more than one redirect hop
- When `studywise doctor` runs
- Then the HTTP client follows at most one redirect
- And the final connection-check result is FAIL unless a 2xx response is reached within that limit

### AC7: Doctor exit code
- Given at least one diagnostic check result is FAIL
- When `studywise doctor` completes
- Then process exit code is non-zero

## Explicit Boundaries
- Do not remove `DiagnosticStatus.Warn` from the enum or reporting pipeline.
- Do not add a new diagnostic status for cancellation in this issue.
- Do not change diagnostic ordering (`config`, `api-key`, `connection`) unless required by existing tests.
- Do not introduce new command-line flags for timeout or redirect limits in this issue.
- Do not bypass `IHttpClientFactory`; use the existing named client setup.

## Risks And Mitigations
- Risk: timeout and explicit cancellation may both surface as `TaskCanceledException`.
  - Mitigation: classify using cancellation-token intent; map timeout to FAIL, but preserve explicit user cancellation as abort.
- Risk: redirect semantics can vary by handler configuration.
  - Mitigation: enforce redirect budget on the named client and verify with focused tests.
- Risk: flaky network-dependent tests.
  - Mitigation: keep unit tests deterministic; rely on controlled test doubles/local servers, not external hosts.
- Risk: mismatch with US-003 base URL precedence.
  - Mitigation: treat `ApplicationConfig.ApiBaseUrl` as contract; open follow-up only if US-003 merge changes precedence.

## Verification
- Build from repository root:
  - `dotnet build`
- Run connection-check unit tests:
  - `dotnet test --filter "FullyQualifiedName~ConnectionDiagnosticCheck"`
- Run doctor integration tests:
  - `dotnet test --filter "FullyQualifiedName~DoctorCommandIntegrationTests"`
- Optional full suite:
  - `dotnet test`

Notes:
- Run commands from repository root to avoid `MSB1003` (no project/solution in current directory).
