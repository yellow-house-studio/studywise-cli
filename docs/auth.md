# Authentication Architecture

## Overview

The Studywise CLI uses **API key authentication** for all requests to the
Studywise API. The key is carried in the `X-Studywise-Api-Key` request header
on every outbound call to the API. Family isolation is enforced server-side
by the API's `ApiKeyAuthenticationHandler` — the CLI only handles transport.

## API Key Authentication

Agents (Robert, Lilly) authenticate with a pre-configured API key. Two
sources are checked, in order:

1. `STUDYWISE_API_KEY` environment variable
2. `apiKey` (or `api_key`) field in `~/.config/studywise/config.json`

Set the env var:

```bash
export STUDYWISE_API_KEY=sk_live_xxx
```

…or add to the config file:

```json
{
  "apiKey": "sk_live_xxx"
}
```

The API key is passed via the `X-Studywise-Api-Key` header to the Studywise
API. The header name is defined as the `StudywiseDefaults.ApiKeyHeaderName`
constant so it lives in one place (`src/Studywise.Cli/Configuration/ApplicationConfig.cs`).

## API Key Transport

The API key is carried by the **SDK's** `ApiKeyAuthenticationHandler`, which
sets the `X-Studywise-Api-Key` header on every outbound request. The CLI
registers the SDK's typed clients with `AddStudywiseClient(baseAddress, apiKey)`
and never touches the header itself — the CLI used to do this via
`ApiKeyTokenProvider` + `ApiKeyDelegatingHandler`, both of which the SDK
superseded.

`ApiKeyAuthenticationHandler` is a no-op when the API key is empty, so the
same client works against endpoints that do not require authentication.

The CLI additionally configures the SDK's clients to **not follow redirects**
(`StudywiseHttpClientExtensions.UseStudywiseDefaults`). The SDK attaches the
API key to every request, so following a 3xx would forward the credential to
the redirect target host. Tracked upstream as
[studywise-api#890](https://github.com/yellow-house-studio/studywise-api/issues/890).

## Auth Verification

`GET /api/v1/auth/verify` is the dedicated probe the API exposes for the
CLI's auth checks. It returns 200 + `{ "userId": "users/<id>", "authMethod": "ApiKey" }`
on success, 401 + `{ "error": "InvalidKey" }` on bad credentials (rewritten
by `AuthVerifyChallengeMiddleware` — no distinction between missing,
invalid, and revoked keys). `studywise doctor --check auth-verify` exercises
this endpoint; `studywise auth verify` (issue #23) is a follow-up that
consumes the same SDK client.

The CLI calls it through the SDK's `AuthClient.VerifyAsync()`, so the route
and the response shape are the SDK's concern. The check maps the outcome onto
its own diagnostic vocabulary:

| Outcome | Diagnostic message |
|---|---|
| 200 | `Auth verify: OK — connected as <userId> (<authMethod>)` |
| 401 | `Auth verify: FAIL — invalid or revoked API key` |
| other status | `Auth verify: FAIL — server returned <code>` |
| timeout | `Auth verify: FAIL — timeout after 5s` |
| unreachable | `Auth verify: FAIL — could not reach the API (<ExceptionType>)` |
| no key configured | `Auth verify: SKIP — no API key configured` |

The SDK throws `HttpRequestException` on a non-success status, and since .NET 5
that exception carries `StatusCode`, so the 401 case stays distinguishable
from other failures.

## Out of Scope (Deliberate)

- **Auth0 device flow / OAuth for human users.** A separate human-user auth
  issue, when we get there. Not part of this CLI's agent use case.
- **API key CRUD** (create / list / revoke). Handled in the dashboard or a
  separate user story.
- **OS keychain / DPAPI credential storage.** Today's file-based config is
  good enough for an agent's dev box; revisit when human users are in
  scope.

## Environment Variables

| Variable | Description |
|----------|-------------|
| `STUDYWISE_API_KEY` | API key for agent authentication |
| `STUDYWISE_API_BASE_URL` | Studywise API base URL (default: `https://api.studywise.io`) |
| `STUDYWISE_CONFIG_PATH` | Override path to `config.json` (default: `~/.config/studywise/config.json`) |
