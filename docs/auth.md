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

## Token Provider

`ApiKeyTokenProvider` implements `ITokenProvider` and returns the configured
API key. `ApiKeyDelegatingHandler` is a `DelegatingHandler` registered on the
named `HttpClient` pipeline that injects the header on every request and
clears any pre-existing `Authorization` so we never mix auth schemes.

## SDK Swap Seam

Commands and diagnostic checks should depend on `IStudywiseTransport`, not
`IHttpClientFactory` directly. Today's `HttpClientStudywiseTransport`
delegates to the named `HttpClient` (which has `ApiKeyDelegatingHandler` in
its pipeline). When `studywise-api` ships a typed SDK
(`Studywise.SDK`/equivalent), only `HttpClientStudywiseTransport` needs to
be replaced — commands and checks stay untouched.

```
IStudywiseTransport (interface)
└── HttpClientStudywiseTransport   # Current: raw HttpClient + handler
    └── [future] StudywiseSdkTransport  # When SDK lands
```

## Auth Verification

`GET /api/v1/auth/verify` is the dedicated probe the API exposes for the
CLI's auth checks. It returns 200 + `{ "userId": "users/<id>", "authMethod": "ApiKey" }`
on success, 401 + `{ "error": "InvalidKey" }` on bad credentials (rewritten
by `AuthVerifyChallengeMiddleware` — no distinction between missing,
invalid, and revoked keys). `studywise doctor --check auth-verify` exercises
this endpoint; `studywise auth verify` (issue #23) is a follow-up that
consumes the same transport.

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
