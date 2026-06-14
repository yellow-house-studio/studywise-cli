# Issue #2 Plan — Auth0 API Keys Authentication

## Summary
Implement API key authentication for CLI agents (Robert, Lilly) via `X-Api-Key` header. The API validates key→user→family→data access chain. No M2M tokens (too broad). Storage via `STUDYWISE_API_KEY` environment variable only.

---

## What
CLI agents authenticate using a pre-configured API key passed via `X-Api-Key` header. The Studywise API validates the key against the user/family chain to enforce family isolation (Robert + Lilly can only access Johanna's children's data).

## Why
- **Family isolation**: Robert + Lilly must NOT access Junis data or other families' data
- **Agent authentication**: CLI agents need a simple, non-interactive auth mechanism (vs Auth0 device flow for humans)
- **Security**: No M2M tokens = no app-level broad access

---

## User Story
Som agent (Robert/Lilly)
Vill jag kunna köra CLI-kommandon med min API-nyckel utan interaktiv inlogning
Så att jag kan automatiskt hantera Studywise-data för Johannas familj

---

## Acceptance Criteria

### AC-1: API Key Storage
- **Given** no API key is configured
- **When** the CLI starts
- **Then** it reads `STUDYWISE_API_KEY` from environment and uses it

### AC-2: Token Provider Interface
- **Given** `ITokenProvider` interface exists
- **When** an HttpClient request is made
- **Then** the `X-Api-Key` header is set from the configured token provider

### AC-3: Family Isolation (API-side)
- **Given** Robert's API key is bound to Johanna's family
- **When** Robert calls `/api/children`
- **Then** only Johanna's children's data is returned (not Junis or others)

### AC-4: Missing API Key Handling
- **Given** `STUDYWISE_API_KEY` is not set or empty
- **When** any CLI command runs
- **Then** an error message is shown: "API-nyckel saknas. Sätt STUDYWISE_API_KEY."

### AC-4b: Invalid/Revoked API Key Handling
- **Given** `STUDYWISE_API_KEY` is set but the API returns 401 Unauthorized
- **When** any CLI command runs
- **Then** an error message is shown: "API-nyckel ogiltig eller återkallad. Kontrollera STUDYWISE_API_KEY."

### AC-4c: Wrong Family Key Handling
- **Given** `STUDYWISE_API_KEY` is set but the API returns 403 Forbidden (family mismatch)
- **When** any CLI command runs
- **Then** an error message is shown: "API-nyckel inte giltig för denna familj. Kontrollera STUDYWISE_API_KEY."

### AC-5: Doctor Command Detects Auth Status
- **Given** a valid API key is configured
- **When** `studywise doctor` runs
- **Then** it shows `[PASS] API-nyckel: OK — finns (maskerad)`
- **And** connection check succeeds with auth

---

## Boundaries (DO NOT)

- **DO NOT** implement Auth0 device flow (future work)
- **DO NOT** implement M2M tokens (too broad)
- **DO NOT** implement interactive login flow
- **DO NOT** create user-facing OAuth/OIDC flows
- **DO NOT** store API keys in files (use env var only)
- **DO NOT** implement Auth0 SDK — only API key header injection

---

## Dependencies

- Requires: Studywise API endpoint supporting `X-Api-Key` header validation
- Requires: API keys bound to user/family in the backend
- Out of scope: API key generation/management (assumes keys pre-configured)

---

## Risks

| Risk | Likelihood | Impact | Mitigation |
|------|------------|--------|------------|
| API doesn't validate family_id from API key | Medium | High | Coordinate with API team; add integration test |
| API key leaked in logs/error messages | Low | High | Mask key in all output; never log full key |
| Agent uses wrong key (e.g., Junis's key) | Low | High | Clear error if family_id mismatch detected |
| Keys rotated while agent running | Low | Medium | CLI reads env on startup; restart required |

---

## Architecture

```
ITokenProvider (interface)
├── ApiKeyTokenProvider    # Reads STUDYWISE_API_KEY env var
└── (future) Auth0TokenProvider

Token Storage: Environment variable STUDYWISE_API_KEY ONLY

HttpClient: Adds X-Api-Key header via DelegatingHandler
```

> **Note on auth folder:** The plan previously mentioned `~/.config/studywise/auth/` folder for token storage. This is removed — API key authentication uses the env var exclusively. Auth file storage (e.g., `~/.config/studywise/auth/token.json`) applies only to Auth0 device flow tokens (future work), not API keys. The `~/.config/studywise/auth/` directory is reserved for Auth0 device flow tokens (future). API key authentication does not write to disk.

### File Plan

**New files:**
- `src/Studywise.Cli/Auth/ITokenProvider.cs` — interface
- `src/Studywise.Cli/Auth/ApiKeyTokenProvider.cs` — env var reader
- `src/Studywise.Cli/Auth/ApiKeyDelegatingHandler.cs` — HTTP handler that adds header

**Modified files:**
- `src/Studywise.Cli/Program.cs` — register token provider + handler
- `src/Studywise.Cli/Configuration/ApplicationConfig.cs` — expose ApiKey property
- `src/Studywise.Cli/Diagnostics/Checks/ApiKeyDiagnosticCheck.cs` — use token provider

**Tests:**
- `test/Studywise.CLI.UnitTests/Studywise.Cli.UnitTests/ApiKeyTokenProviderTests.cs`
- `test/Studywise.CLI.UnitTests/Studywise.Cli.UnitTests/ApiKeyDelegatingHandlerTests.cs`

---

## Verification

### Automated Tests
```bash
# Unit tests
dotnet test test/Studywise.CLI.UnitTests/Studywise.Cli.UnitTests/Studywise.CLI.UnitTests.csproj --filter "ApiKey"

# Integration tests (with mocked HTTP)
dotnet test test/Studywise.CLI.IntegrationTests/Studywise.Cli.IntegrationTests/Studywise.CLI.IntegrationTests.csproj

# E2E tests
dotnet test test/Studywise.CLI.E2ETests/Studywise.CLI.E2ETests.csproj
```

> **Note:** E2E tests require `STUDYWISE_API_KEY` to be set in the environment.

### Manual Verification
```bash
# With valid key
export STUDYWISE_API_KEY=sk_live_test123
dotnet run --project src/Studywise.Cli/Studywise.Cli.csproj -- doctor

# Expected output:
# [PASS] API-nyckel: OK — finns (maskerad)

# Without key
unset STUDYWISE_API_KEY
dotnet run --project src/Studywise.Cli/Studywise.Cli.csproj -- doctor

# Expected output:
# [FAIL] API-nyckel: FAIL — saknas i environment variable
```

### curl test (API contract — family isolation)
```bash
# Verify API accepts X-Api-Key header and enforces family isolation
# Using /api/children (authenticated endpoint that returns family-scoped data)
curl -H "X-Api-Key: sk_live_test123" https://api.studywise.io/api/children

# Expected: 200 OK with only Johannas children's data (family isolation verified at API level)
# If key is wrong family: 403 Forbidden
# If key is missing/invalid: 401 Unauthorized
```

> **Note on family isolation:** The `/health` endpoint does not validate family_id from the API key. Family isolation is validated at the API level on authenticated endpoints like `/api/children`. Use `/api/children` (or any `/api/*` endpoint) to verify family-scoped access.

---

## Implementation Steps

1. Create `Auth/ITokenProvider.cs` interface
2. Create `Auth/ApiKeyTokenProvider.cs` implementation (reads env var)
3. Create `Auth/ApiKeyDelegatingHandler.cs` that injects `X-Api-Key` header
4. Update `Program.cs` to register token provider and configure HttpClient with handler
5. Update `ApplicationConfig.cs` to expose `ApiKey` property for diagnostics
6. Update `ApiKeyDiagnosticCheck.cs` to use token provider for validation
7. Add unit tests: `ApiKeyTokenProviderTests.cs`, `ApiKeyDelegatingHandlerTests.cs`
8. Add integration test: verify header is sent
9. Run `dotnet build` + `dotnet test`
10. Manual verification with `studywise doctor`

---

## Notes

- API base URL is `https://api.studywise.io` (not `.se` — see `StudywiseDefaults`)
- Mask API key in all output (show first 4 chars + `***` + last 4)
- Auth folder is already documented in `docs/auth.md` — **update `docs/auth.md` when this feature is implemented** to reflect that API keys use env var only (no auth file)

---

## Doc Updates Required After Implementation

| File | Update |
|------|--------|
| `docs/auth.md` | Add note that API keys use `STUDYWISE_API_KEY` env var only; auth folder is for future Auth0 device flow |