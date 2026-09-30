# Developer Environment Setup

## Status
**Datum:** 2026-09-29 (updated after NUnit + E2E-removal work)
**Typ:** Developer Guide
**Projekt:** Studywise CLI

---

## Prerequisites

- .NET 10 SDK
- Git
- `reportgenerator` .NET global tool (for coverage merge in
  `verify.sh`) — install via `./scripts/setup-env.sh reportgenerator`

The CLI's tests build a WireMock server in-process and never dial the
real Studywise API. No `STUDYWISE_API_KEY` secret is needed for any
local or CI run. See `docs/testing/testing-strategy.md`.

---

## Tools (optional)

The following tools are auto-installed by `scripts/setup-env.sh` and
improve the local dev loop. None are required for `dotnet build` /
`dotnet test` to succeed — verify.sh auto-skips them with a clear
message if they are missing.

### gitleaks

Defensive secret-scan layer. `verify.sh` Phase 5a scans the working
tree for known secret patterns (AWS keys, GitHub tokens, Stripe live
keys, etc.) and fails the run if any are found. Install via
`./scripts/setup-env.sh gitleaks` (mac: `brew install gitleaks`,
Linux: downloads the static binary to `/usr/local/bin`).

### actionlint

Workflow-file lint. `verify.sh` Phase 5b lints
`.github/workflows/*.yml,*.yaml` and fails on malformed workflow
syntax. Install via `./scripts/setup-env.sh actionlint` (mac:
`brew install actionlint`, Linux: downloads the v1.7.7 binary).

### Node.js

`verify.sh` Phase 5c runs cspell via `npx cspell@10` against
`src/**/*.cs`. The cspell package is downloaded on first invocation
and cached by npm — no separate install. The Node prerequisite is
covered by `./scripts/setup-env.sh node` if missing.

---

## Running Tests

The local CI mirror is `scripts/verify.sh`. See
`scripts/README.md` for the full scope matrix.

```bash
# Fast inner loop (unit tests only)
./scripts/verify.sh --scope=unit

# Pre-push gate (unit + integration, mirrors what the pre-push hook runs)
./scripts/verify.sh --scope=integration

# Full CI mirror (adds the coverage gate + baseline auto-raise)
./scripts/verify.sh --scope=full

# Category-filtered runs (when you only want one layer)
dotnet test --filter "Category=Unit"
dotnet test --filter "Category=Integration"
```
