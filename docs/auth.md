# Authentication Architecture

## Overview

The Studywise CLI supports two authentication methods:

1. **API Key** (for agents: Robert, Lilly)
2. **Auth0 Device Flow** (for end users - future work)

## API Key Authentication

Agents use a pre-configured API key from environment only:

```bash
export STUDYWISE_API_KEY=sk_live_xxx
```

The API key is passed via the `X-Api-Key` header to the Studywise API.
No API keys are written to disk.

### Token Provider

`ApiKeyTokenProvider` implements `ITokenProvider` and reads `STUDYWISE_API_KEY`.

When key is missing, CLI commands surface:

`API-nyckel saknas. Sätt STUDYWISE_API_KEY.`

## Auth0 Device Flow (Future)

For human users, Auth0 device flow provides interactive authentication:

```bash
studywise auth login
-> Opens browser for Auth0 login
-> Caches token to disk (~/.config/studywise/auth/token.json)
```

## Architecture

```
ITokenProvider (interface)
├── ApiKeyTokenProvider     # Reads STUDYWISE_API_KEY from env var
└── (future) Auth0TokenProvider

API key storage
└── Environment variable only (STUDYWISE_API_KEY)

Auth0 device flow token storage (future)
└── ~/.config/studywise/auth/token.json
```

## Environment Variables

| Variable | Description |
|----------|-------------|
| `STUDYWISE_API_KEY` | API key for agent authentication |
| `STUDYWISE_AUTH0_DOMAIN` | Auth0 domain (e.g., `yhs-studywise.auth0.com`) |
| `STUDYWISE_AUTH0_CLIENT_ID` | Auth0 client ID |
| `STUDYWISE_AUTH0_CLIENT_SECRET` | Auth0 client secret |
| `STUDYWISE_AUTH0_AUDIENCE` | Auth0 audience/API identifier |
| `STUDYWISE_API_BASE_URL` | Studywise API base URL (default: `https://api.studywise.io`) |
