# Plan: Issue #23 — `studywise auth verify`

## What
A new `studywise auth verify` CLI command that validates the configured API key against the API and returns the associated `family_id` (user account ID).

## Why
Agents (Robert, Lilly) need to verify their API key gives access to the correct family before running other commands. Currently `doctor` only checks that a key exists in config — not that it's valid or scoped correctly.

## Acceptance Criteria

### Given a configured API key in `~/.config/studywise/config.json` or `STUDYWISE_API_KEY`
### When the agent runs `studywise auth verify`
### Then
- CLI sends the API key to `GET /api/v1/users/me` with `X-Api-Key: <key>` header
- On success: prints `Connected as: <user_id>` to stdout and exits 0
- On 401/unauthorized: prints `Error: Invalid API key` to stderr and exits 1
- On network error: prints `Error: Could not reach API` to stderr and exits 1

### Given no API key is configured
### When the agent runs `studywise auth verify`
### Then
- Prints `Error: No API key configured` to stderr and exits 1

## Verification

### Automated tests
- **Unit test**: `AuthVerifyCommandHandlerTests` — mock HTTP, assert output for valid key, invalid key, missing key
- **Unit test**: `AuthVerifyCommandTests` — verify command registration and argument parsing

### Manual commands
```bash
# Valid key → shows connected user
studywise auth verify
# → Connected as: users/<uuid>

# Invalid key → error
STUDYWISE_API_KEY=bad ./studywise auth verify
# → Error: Invalid API key

# No key → error
studywise auth verify
# → Error: No API key configured
```

### Manual curl (API side)
```bash
# Valid request
curl -H "X-Api-Key: sk_live_xxx" https://api.studywise.io/api/v1/users/me

# Expected 200 response (after API key middleware is live)
# {"userId": "users/<uuid>", "userType": "..."}

# Invalid key
curl -H "X-Api-Key: bad" https://api.studywise.io/api/v1/users/me
# Expected 401
```

## Boundaries

### What's NOT included
- Creating, listing, or revoking API keys (separate stories: US-001, US-004, US-005)
- Auth0 device flow for human users (future work)
- Interactive login prompts
- Config file creation or update
- Storing the verified identity for future commands (auth is re-verified on each call)
- `studywise auth login` subcommand (separate issue)

## Dependencies

| Dependency | Owner | Status | Blocking? |
|------------|-------|--------|-----------|
| API key middleware (`X-Api-Key` header validation) | studywise-api | **Not started** | **YES — blocks CLI auth verify** |
| `GET /api/v1/users/me` accepts `X-Api-Key` auth | studywise-api | Needs middleware first | **YES** |
| `family_id` concept exists in API | studywise-api | No Family entity yet; `user_id` from `UserAccount` is the identifier | Clarified: use `user_id` |

> **Note on `family_id`:** The issue mentions `family_id` but no Family entity exists in the current API domain. The UserAccount's `Id` is the correct identifier to return. CLI will display `user_id` (e.g. `users/<uuid>`) — the term "family" is user-facing language for the same concept.

## Risks

| Risk | Likelihood | Impact | Mitigation |
|------|------------|--------|------------|
| API key middleware not ready | High | Blocks entire feature | CLI implementation done first; test against stub/mock API |
| `X-Api-Key` header not sent by HTTP client | Medium | Command always fails | Ensure `HttpClient` is configured to send `X-Api-Key` from config |
| API returns non-401 for invalid key | Medium | Misleading error message | Map specific HTTP status codes explicitly |
| No network connectivity in CI | Low | E2E tests fail | Use Dev Proxy for E2E tests |

## Designed But Not Built

| Item | Reason |
|------|--------|
| Auth0 device flow login (`studywise auth login`) | Separate track for human users |
| API key CRUD (create/list/revoke) | Separate user stories US-001, US-004, US-005 |
| Cached auth token | Re-verification on each call is acceptable for agents |

## Implementation Notes

### CLI side
- New command: `src/Studywise.Cli/Commands/Auth/AuthVerifyCommand.cs`
- New handler: `src/Studywise.Cli/Commands/Auth/AuthVerifyCommandHandler.cs`
- New options (if needed): `src/Studywise.Cli/Commands/Auth/AuthVerifyCommandOptions.cs`
- Register in `Program.cs` alongside `DoctorCommand`
- HTTP client must include `X-Api-Key` header (currently not configured — fix as part of this)

### API side (studywise-api, separate PR)
- API key middleware: validate `X-Api-Key` header, set `ICurrentUser` identity
- `GET /api/v1/users/me` already exists — will work once middleware is live
- Response model `CurrentUserVM` already has `UserId` field — reuse it

### HTTP header requirement
The existing HTTP client in `Program.cs` does NOT currently send `X-Api-Key`. This must be added:
```csharp
services.AddHttpClient(StudywiseDefaults.ApiName, client =>
{
    client.BaseAddress = new Uri(config.ApiBaseUrl);
    client.DefaultRequestHeaders.Add("User-Agent", config.UserAgent);
    if (!string.IsNullOrWhiteSpace(config.ApiKey))
        client.DefaultRequestHeaders.Add("X-Api-Key", config.ApiKey);
});
```
