# Plan: Issue #2 — `X-Studywise-Api-Key` auth transport

## What

CLI agents (Robert, Lilly) authenticate to the Studywise API by sending the
configured API key in the `X-Studywise-Api-Key` header on every request.
This PR adds the transport layer (delegating handler + DI wiring + diagnostic
that actually exercises auth) so commands can use a single named `HttpClient`
and get authenticated for free. It does **not** add new user-facing commands
(`studywise auth verify`, issue #23, is a follow-up that consumes this).

## Why

- **Family isolation is enforced server-side.** The CLI only handles auth
  transport; the API's `ApiKeyAuthenticationHandler`
  (`src/Studywise.API/Authentication/ApiKeyAuthenticationHandler.cs:37`)
  hashes the key, looks up the `UserAccount`, and walks the user → family
  chain.
- **Today the CLI does not send any auth header.** Any future command that
  hits a protected endpoint will silently 401. Building this now keeps every
  follow-up command trivial.
- **A typed SDK is coming from `studywise-api`.** We design the boundary so
  swapping the raw-HTTP implementation for the SDK is a single class
  replacement, not a per-command rewrite.

## What is already on `main`

- `ApplicationConfig` reads `STUDYWISE_API_KEY` from env, falling back to
  `~/.config/studywise/config.json` (`apiKey` / `api_key`). Env var wins.
- `ApiKeyDiagnosticCheck` reports PASS/FAIL on whether a key is configured.
- `DoctorCommandHandler` wires the check into `studywise doctor` and supports
  `--check api-key`.

## What this PR adds

### `src/Studywise.Cli/Auth/`
- `ITokenProvider` — abstraction over "give me the credential for the next
  request". Survives any future credential store (env, config file, OS
  keychain, Vault).
- `ApiKeyTokenProvider` — concrete impl that returns the configured key.
  Throws `InvalidOperationException` with an English message when the key is
  missing or whitespace.
- `ApiKeyDelegatingHandler : DelegatingHandler` — reads the token from the
  provider and sets the request header. Also clears any pre-existing
  `X-Studywise-Api-Key` (defensive against cached requests) and clears
  `Authorization` so we never accidentally mix auth schemes.

### `src/Studywise.Cli/Http/` — the SDK swap seam
- `IStudywiseTransport` — single method
  `Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)`.
  Every future command takes this seam, not `IHttpClientFactory` directly.
- `HttpClientStudywiseTransport` — current implementation. Resolves the named
  `HttpClient` from `IHttpClientFactory`, calls `SendAsync` on it. When the
  `Studywise.SDK` package lands, this class is the only thing replaced.

### `src/Studywise.Cli/Diagnostics/Checks/AuthVerifyDiagnosticCheck.cs`
- New check, runs in `doctor --check auth-verify` and as part of `--check all`
  when a key is configured.
- Probes `GET /api/v1/auth/verify`
  (`src/Studywise.API/Controllers/Auth/AuthController.cs:47`).
- **PASS** — 200 with body `{"userId":"users/<id>","authMethod":"ApiKey"}`.
  Message renders `Auth verify: OK — connected as users/<id> (ApiKey)`.
  The userId is the full deterministic id (no extra masking; it's not a secret).
- **FAIL — invalid/revoked/missing key** — 401 with body
  `{"error":"InvalidKey"}` (rewritten by
  `AuthVerifyChallengeMiddleware`). Message:
  `Auth verify: FAIL — invalid or revoked API key`.
- **FAIL — wrong family** — the API does not distinguish wrong-family from
  invalid key on this endpoint (intentional, per
  `AuthVerifyChallengeMiddleware.cs:11-13`). Doctor treats 401 as the
  single failure mode. A future "wrong family" specific check can land if
  the API ever surfaces a distinct status for it.
- **FAIL — network/timeout** — same shape as `ConnectionDiagnosticCheck`
  (`timeout after 5s`, `could not reach /api/v1/auth/verify`).
- When no key is configured: returns SKIP (`DiagnosticStatus.Warn`) with
  message `Auth verify: SKIP — no API key configured`. Doctor exit code
  stays 0 so this isn't a failure.

### `src/Studywise.Cli/Configuration/StudywiseDefaults.cs`
- Add `ApiKeyHeaderName = "X-Studywise-Api-Key"` constant. Centralized so the
  header name lives in one place — `ApiKeyDelegatingHandler` and tests both
  reference it.

### `src/Studywise.Cli/Program.cs`
- Register `ITokenProvider` (singleton) → `ApiKeyTokenProvider(config)`.
- Add `ApiKeyDelegatingHandler` to the named `HttpClient` pipeline via
  `.AddHttpMessageHandler<ApiKeyDelegatingHandler>()`.
- Register `IStudywiseTransport` (transient) → `HttpClientStudywiseTransport`.
- Set `AllowAutoRedirect = false` on the primary handler to prevent
  `X-Studywise-Api-Key` leakage across 3xx responses (security fix from the
  closed PR #18's review; lands here for the first time on a current `main`).

### `src/Studywise.Cli/Commands/Doctor/DoctorCommandHandler.cs`
- Add `auth-verify` to the `--check` switch alongside `config`, `api-key`,
  `connection`. Default `all` adds the new check.

### `src/Studywise.Cli/Studywise.Cli.csproj`
- Drop the unused `Auth0.AuthenticationApi` package reference. There is zero
  Auth0 code in this repo and none planned.

### `docs/auth.md`
- Correct the header name from `X-Api-Key` to `X-Studywise-Api-Key`.
- Document the `IStudywiseTransport` seam so the next agent who picks up an
  SDK-replacement task knows where to start.
- Drop the "Auth0 Device Flow (Future)" section — that work is human-user
  auth and belongs on a separate issue.

## Tests

### Unit (NUnit 5, `test/Studywise.CLI.UnitTests/`)
- `ApiKeyTokenProviderTests`:
  - Returns the configured key when present
  - Throws `InvalidOperationException` with the English missing-key message
    when null / empty / whitespace
  - Never logs the key value (constructor doesn't leak; error message does
    not contain the key)
- `ApiKeyDelegatingHandlerTests`:
  - Adds `X-Studywise-Api-Key` header with the provider's token
  - Removes any pre-existing `X-Studywise-Api-Key` header before adding
    (defensive)
  - Clears `Authorization` header to avoid scheme mixing
  - Calls the inner handler exactly once
- `HttpClientStudywiseTransportTests`:
  - Delegates to the named `HttpClient`
  - Forwards the cancellation token
  - Propagates the inner exception
- `AuthVerifyDiagnosticCheckTests`:
  - 200 → PASS, message contains userId and authMethod
  - 401 with `{"error":"InvalidKey"}` → FAIL, message contains
    "invalid or revoked"
  - 401 with empty body → FAIL (still detected — we key off status code, not body)
  - 500 / 503 → FAIL, message contains status code
  - `TaskCanceledException` → FAIL with timeout message
  - `HttpRequestException` → FAIL with unreachable message
  - No key configured → SKIP with "no API key configured"
- `ApplicationConfigTests` — extend: confirm the new `ApiKeyHeaderName`
  constant equals `"X-Studywise-Api-Key"`.

### Unit updates (existing tests, English messages)
- `ApiKeyDiagnosticCheckTests` — replace `"API-nyckel: OK ..."` /
  `"API-nyckel: FAIL ..."` assertions with English equivalents
  (`"API key: OK — present (masked)"` / `"API key: FAIL — missing or empty in config"`).
- `TextDiagnosticReportFormatterTests` — same replacement.
- `DoctorCommandHandlerTests` — same replacement; also extend the
  `--check` switch test cases with `auth-verify` and update the "all" test
  to expect 4 checks instead of 3.

### Integration (NUnit 5, WireMock, `test/Studywise.CLI.IntegrationTests/`)
- `Doctor_AuthVerify_ReturnsPassWhenApiAcceptsKey` — WireMock stubs
  `GET /api/v1/auth/verify` to return 200 + `{"userId":"users/abc","authMethod":"ApiKey"}`;
  run doctor via the existing helper; assert PASS message contains the
  userId.
- `Doctor_AuthVerify_ReturnsFailWhenApiRejectsKey` — stub 401 +
  `{"error":"InvalidKey"}`; assert FAIL message.
- `Doctor_AuthVerify_SkipsWhenNoKey` — empty config; assert SKIP / WARN,
  overall `IsSuccess` stays true.
- `Doctor_ConnectionCheck_FailsWhenHealthRedirectsMoreThanOnce` — keep;
  unchanged behavior.
- Extend the existing `Doctor_TextFormatting_ProducesReadableText` and
  `Doctor_JsonFormatting_ProducesJsonReport` tests to assert the new check
  shows up in the report (4 checks instead of 3, name `"auth-verify"`).

## Error messages (English only — new repo standard)

| Check | Outcome | Message |
|-------|---------|---------|
| `api-key` | key present | `API key: OK — present (masked)` |
| `api-key` | missing | `API key: FAIL — missing or empty in config` |
| `auth-verify` | 200 | `Auth verify: OK — connected as users/<id> (ApiKey)` |
| `auth-verify` | 401 | `Auth verify: FAIL — invalid or revoked API key` |
| `auth-verify` | 5xx | `Auth verify: FAIL — server returned <code>` |
| `auth-verify` | timeout | `Auth verify: FAIL — timeout after 5s reaching /api/v1/auth/verify` |
| `auth-verify` | unreachable | `Auth verify: FAIL — could not reach /api/v1/auth/verify (<ExceptionType>)` |
| `auth-verify` | no key | `Auth verify: SKIP — no API key configured` (WARN) |

This is a **breaking change** for anyone parsing the Swedish strings. Worth a
line in the PR description and a CHANGELOG entry once the CHANGELOG file
exists (it doesn't yet — also out of scope).

## Security notes

- `AllowAutoRedirect = false` on the authenticated `HttpClient` so
  `X-Studywise-Api-Key` cannot leak across a 3xx to a different host. This
  is the security fix flagged in PR #18's review that never landed on
  current `main`.
- API key values must never appear in logs, exception messages, or stdout.
  `ApiKeyTokenProvider.GetToken()` only throws an English message with no
  key value; the diagnostic checks render PASS/FAIL strings without
  echoing the key.
- The userId returned by `/api/v1/auth/verify` is a non-secret identifier
  and may be rendered.

## Boundaries (in scope / out of scope)

### In scope
- Auth transport (`Auth/` folder, `HttpClient` wiring)
- New `AuthVerifyDiagnosticCheck` exercising the `/api/v1/auth/verify`
  endpoint
- `IStudywiseTransport` seam for future SDK swap
- English doctor output (repo standard)
- Drop unused `Auth0.AuthenticationApi` package
- Update `docs/auth.md` and delete the stale
  `docs/plans/2-auth0-api-keys.md`

### Out of scope (deliberate)
- Building `studywise auth verify` as a user-facing command — that's
  issue #23, a separate PR that consumes this transport.
- Creating, listing, or revoking API keys — separate user stories.
- Auth0 device flow / OAuth for human users — deferred to a separate
  human-user auth issue.
- Replacing `ApplicationConfig.ReadApiKeyFromConfigFile`'s file-IO with a
  secure credential store (Keychain, libsecret, Windows DPAPI). Future
  work.
- Key rotation UI.

## Verification

- `./scripts/verify.sh --scope=fast` (the pre-push hook runs this
  automatically; don't bypass).
- `./scripts/verify.sh --post-comment` AFTER `gh pr create`, wait for the
  `[verify] ✅ Posted sticky PR comment to PR #N` line.
- Manual smoke test with a real key (out of CI scope): `studywise doctor`
  with a valid key → PASS; with a bogus key → 401 FAIL.

## Tracking

- Closes the auth-transport half of issue #2.
- Supersedes `docs/plans/2-auth0-api-keys.md` (deleted in this PR).
- Supersedes PR #18 (closed as superseded, see its close comment).
- Unblocks issue #23 (`studywise auth verify` command).
