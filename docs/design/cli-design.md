# Studywise CLI design

> System-wide design record for the `studywise` CLI. New commands must be
> consistent with this doc. Where the doc is silent, the precedent is the
> existing merged code (`studywise doctor` and any merged siblings); where
> this doc and the code disagree, **this doc is the source of truth** and
> the code is wrong until a new ADR closes the gap.

This is a living document. Decisions are recorded as ADRs (one per
section). Each section has an **Acceptable-change rules** subsection
that defines what kinds of edits may be made without raising a new
ADR.

---

## Table of contents

- [§ 1 Scope and audience](#1-scope-and-audience)
- [§ 2 Command tree shape](#2-command-tree-shape)
- [§ 3 Input contract](#3-input-contract)
- [§ 4 Output contract](#4-output-contract)
- [§ 5 Error contract](#5-error-contract)
- [§ 6 HTTP transport](#6-http-transport)
- [§ 7 SDK adoption](#7-sdk-adoption)
- [§ 8 Retry and idempotency](#8-retry-and-idempotency)
- [§ 9 Configuration and auth](#9-configuration-and-auth)
- [§ 10 Testing contract](#10-testing-contract)
- [§ 11 Documentation rules](#11-documentation-rules)
- [§ 12 Acceptable-change rules (master)](#12-acceptable-change-rules-master)
- [§ 13 Decision log](#13-decision-log)
- [§ 14 Open questions](#14-open-questions)
- [§ 15 Cross-repo coupling](#15-cross-repo-coupling)

---

## 1. Scope and audience

### Status

**Adopted.** Decided on 2026-10-18 in the
`chore/design-cli-design` PR.

### Decision

The `studywise` CLI has **two audiences** and must satisfy both with the
same commands:

1. **AI agents** (primary). Deterministic, parseable, flag-driven, no
   prompts, no spinner tricks, no TTY-only escape hatches.
2. **Human operators** (secondary). `--help` is honest, errors name the
   fix, `--json` is available when humans need to pipe.

"AI-friendly" and "human-friendly" must converge on the same code path.
There is no "interactive mode" branch. If a feature cannot serve both,
it is rejected.

### Why

- AIs are the dominant caller for any Studywise API integration in 2026.
- Humans using the CLI are already operating close to a terminal; they
  can pass flags.
- A two-mode CLI (interactive vs `--non-interactive`) duplicates the
  command surface and creates a class of bugs that only the AI path
  triggers in production.

### Acceptable-change rules

- **Adding** a new audience to this section is a *breaking* change for
  the contracts in § 3–5 (they were calibrated for the two listed
  audiences). Requires a new ADR.
- **Renaming** an audience label is a *non-breaking* clarification.

### Open questions

_None._

---

## 2. Command tree shape

### Status

**Adopted.** Precedent: `studywise doctor` (only merged command at
v2.2.0 / 2026-10-18).

### Decision

```
studywise <noun> <verb> [--flags]

# current
studywise doctor [--check <name>] [--json]
```

Rules:

- Every command lives under a **noun**: `<noun>` is a singular resource
  name (`book`, `word`, `schoolYear`, ...), not a plural (`books`).
- Nouns and verbs are separated by exactly one space. No `--` style
  pseudo-nouns.
- `doctor` is the one allowed exception — it is the diagnosable
  self-check verb and intentionally has no noun.
- Reserved top-level commands: `help`, `doctor`, `version`. They MUST
  NOT be repurposed as nouns.
- Subcommands are mandatory above one verb per noun. If a noun has only
  one verb today and might gain more, the verb is still required
  (`studywise book add`, never `studywise book-add`).
- Command ordering in `--help` is alphabetical by noun, then by verb.

### Why

- Singular noun matches the README's `studywise auth login` /
  `studywise words list` pattern and the SDK surface (one `*Client` per
  resource).
- Mandatory verbs future-proof the tree: `studywise book add` →
  `studywise book list` later does not break an existing `--list`
  global flag.
- `doctor` as an exception was grandfathered because it predates the
  noun/verb rule and represents a different surface (diagnostics, not
  a CRUD verb).

### Acceptable-change rules

- **Renaming** a noun or verb is *breaking*. Requires a new ADR and an
  alias shim for one minor release.
- **Adding** a noun or verb is *non-breaking*.
- **Reordering** entries in `--help` is *non-breaking*.

### Open questions

_None._

---

## 3. Input contract

### Status

**Adopted.** Decided on 2026-10-18.

### Decision

Inputs are **flags only**. The CLI does not prompt under any circumstance.

- Required fields are required flags. Missing them → usage error.
- Positional arguments are forbidden.
- Arrays are repeated flags (`--author firstName=Bob,lastName=Smith`
  is forbidden; instead `--author "firstName=Bob,lastName=Smith"` or
  a small JSON variant is used; see open question § 14.O-A).
- For structured input that does not map to one repeated flag, the
  CLI accepts the same JSON it would send to the API. Two ways
  (`--from-file <path>` and `--from-stdin`) are both supported; piping
  is preferred for agents.
- `--help` documents every flag, its type, its required-ness, and an
  example. CI fails if any registered command lacks an example.

### Why

- Prompts are TTY-dependent, block agents, and produce non-reproducible
  request payloads (no record of what the user typed).
- Flags make every request replayable from a shell history or an
  agent's tool transcript.
- `--help` doubles as the agent's reference document; agents without
  training data on a 2026-Q4 flag should still learn it from the help
  text.

### Acceptable-change rules

- **Adding** a new optional flag is *non-breaking*.
- **Removing** a flag is *breaking*. Requires an ADR + a deprecation
  notice in `--help` for at least one release.
- **Changing** a flag's *type* is *breaking*.
- **Tightening** validation (reject what was previously accepted) is
  *breaking*. **Loosening** is *non-breaking*.

### Open questions

- **§ 14.O-A**: Concretely define the syntax for repeated/nested input
  (e.g. `--author` for `CreateBookRequest.authors[]`). Candidates:
  comma-separated `key=value`, repeated `--author` with a mini-DSL,
  or mandatory JSON for any nested object. Decision deferred until the
  first command that needs it lands.

---

## 4. Output contract

### Status

**Adopted.** Decided on 2026-10-18.

### Decision

Two output modes, switched per-invocation via `--json`:

| Mode | Default (no flag) | `--json` |
|---|---|---|
| Success | One human-readable line on **stdout** | Structured record on **stdout** (CLI-normalized shape, see § 4.2) |
| Failure | One human-readable line on **stderr** (see § 5) | Same human line, plus structured envelope on **stderr** |
| Verbose | Tabs/columns when stdout is a TTY, line-delimited otherwise | n/a — `--json` is the verbose mode |

### 4.1 Channels

- **stdout** carries the requested data on success. It is the contract.
- **stderr** carries logs, progress, and errors. Agents MUST be able to
  redirect stdout to a pipe and never see a stray error.
- Diagnostic chatter (e.g. retry attempts, rate-limit backoff) goes to
  **stderr**, never stdout.
- The CLI never writes a TTY-only escape code (`\x1b[...]`) to stdout
  when stdout is not a TTY. The terminal is detected via
  `Console.IsOutputRedirected`, not via env vars.

### 4.2 `--json` shape

`--json` emits a **CLI-normalized** record (a stable, CLI-owned type
per command), not the raw API response. The shape is documented in the
command's `docs/commands/<noun>-<verb>.md`. Example: the planned
`book add --json` output is a `CreateBookResult` record (id, title,
authors, publisher, placeOfPublication, year, ISBNs, edition,
createdAtUtc).

Rationale: the API can evolve. The CLI contract is what agents
integrate against; pinning to API bytes couples the agent to every
server-side refactor.

### 4.3 Stable fields

Each command documents its "stable field set". Adding a field is
non-breaking. Removing or renaming a field is breaking.

### Acceptable-change rules

- **Adding** a field to a `--json` record is *non-breaking*.
- **Removing/renaming** a field is *breaking*.
- **Switching** a field's JSON type is *breaking* (number ↔ string).
- **Changing** success-channel from stdout to stderr (or vice versa)
  is *breaking*.

### Open questions

- **§ 14.O-B**: Should the CLI ever support `--format csv|table|json`
  beyond `--json`? Defer until a second format is asked for.

---

## 5. Error contract

### Status

**Adopted.** Decided on 2026-10-18.

### Decision

Errors are written to **stderr** in two layers:

### 5.1 Human line (always present)

One line, no leading emoji, the verb in lower-case, the noun when
relevant, the cause, and the corrective hint. Examples:

```
studywise book add: <title> is required (use --title "<book title>")
studywise book add: HTTP 409 Conflict: an ISBN is already associated with a different book
studywise book add: connection to <baseUrl> failed: <underlying error message>
studywise book add: timed out after 30s waiting on <baseUrl>
```

The human line format is stable. Adding new positional segments is
non-breaking; moving the cause and the hint around is breaking.

### 5.2 Structured envelope (`--json` only)

When `--json` is passed and the command fails, the CLI ALSO emits a
machine-readable record on **stderr**, distinct from the human line:

```json
{
  "exitCode": 1,
  "error": {
    "kind": "validation|conflict|unauthorized|notFound|forbidden|rateLimit|server|network|timeout|usage",
    "message": "<same text as the human line, no leading 'studywise book add:'>",
    "request": {
      "method": "POST",
      "path": "/api/v1/books"
    },
    "details": <command-specific, see each command's doc>
  }
}
```

`kind` is a stable enum (additions are non-breaking; renames are
breaking). `details` shape is per-command and is documented under
`docs/commands/<noun>-<verb>.md`.

### 5.3 Exit codes

| Code | Meaning |
|---|---|
| `0` | Success |
| `1` | Operational failure (API rejected it, network failed, timeout) |
| `2` | Usage error (missing/invalid flag, etc.) — System.CommandLine default |
| `64`–`78` | Reserved; do not use. (BSD sysexits.h range; never collide.) |

The CLI never exits `0` on failure, and never exits non-zero on success.

### Acceptable-change rules

- **Adding** a new `kind` to the error envelope is *non-breaking*.
- **Renaming** a kind is *breaking*.
- **Changing** the exit-code map above is *breaking*.

### Open questions

- **§ 14.O-C**: Should we include a `retryable: true|false` boolean on
  the structured envelope? The current SDK and API give no
  server-cooperative signal; v1 says "no", revisit when the API grows
  `Retry-After` support on the SDK side.

---

## 6. HTTP transport

### Status

**Adopted.** Decided on 2026-10-18.

### Decision

There is exactly one HTTP abstraction in the CLI: the SDK package
(see § 7). The historical seam (`IStudywiseTransport` returning
`HttpResponseMessage`) is removed once the SDK is wired.

Layer order (low → high):

```
HTTP wire
  ↑ HttpClient (SDK-named, e.g. "AuthClient")
  ↑ ApiKeyAuthenticationHandler  (SDK ships this; sets X-Studywise-Api-Key)
  ↑ HttpTypedClient               (SDK; throws HttpRequestException on non-success)
  ↑ <Resource>Client              (SDK; e.g. BooksClient.CreateAsync)
  ↑ Command handler               (CLI; translates exceptions → exit code + envelope)
```

The CLI does **not** wrap the SDK in its own "typed API client"
interface. Every CLI command depends on `StudywiseClient` (the SDK's
root) or the specific subclient; the handler resolves from DI.

### Why

- The SDK package already does the right thing (typed responses, no
  manual JSON, typed `HttpRequestException` on failure). Wrapping it
  would mean rewriting it locally and then re-syncing each SDK
  release.
- The boundary is small enough that the test seam is an `HttpClient`
  with a request handler (WireMock + StubHandler), not a mocked
  interface.

### Acceptable-change rules

- **Re-introducing** `IStudywiseTransport` or any wrapper interface
  *is* a *breaking* change. It must be justified with an ADR.

### Open questions

_None._

---

## 7. SDK adoption

### Status

**Adopted.** Decided on 2026-10-18.

### Decision

The CLI consumes **`YellowHouseStudio.Studywise.Sdk`** (the package
published by `studywise-api`) at the version pinned in
`Directory.Packages.props` (Central Package Management). The CLI
does **not** declare its own request/response DTOs; every wire type
is from the SDK.

Updates:

- The CLI tracks SDK tags, not `main`. Bumping the SDK is its own PR
  (small, mechanical), gated on `dotnet build` + the existing test
  suite against the new WireMock fixtures.
- When the SDK ships a new resource client, the CLI does not
  auto-wire it. Each CLI command is its own PR with its own review.

### Why

- Two repos declaring the same DTO type leads to drift within weeks.
  Using the SDK eliminates that drift by construction.
- Manual JSON in the CLI did not earn its keep the first time around
  (only one endpoint hit, all inline). Pulling in the SDK gives
  typed DTOs, a real `CancellationToken` story, and a future path for
  resilience/polly that we don't have to write twice.

### Acceptable-change rules

- **Bumping** the SDK version in `Directory.Packages.props` is
  *non-breaking* provided the version is on the same major.
- **Bumping** a major version is *breaking* for the CLI surface
  (DTO renames, method signature changes). Requires an ADR.
- **Forking** or vendoring the SDK is *breaking* and requires an ADR.

### Open questions

- **§ 14.O-D**: When the SDK ships `IStudywiseResiliencePipeline`
  (per the SDK README's roadmap), the CLI should wire it. The exact
  flag surface (`--retry`, `--max-retries N`) is deferred.

---

## 8. Retry and idempotency

### Status

**Adopted.** Decided on 2026-10-18.

### Decision

The CLI **does not auto-retry**. Every command is one round-trip.
Network timeouts, 5xx responses, and `429 Too Many Requests` are
returned as errors (exit 1) and the agent decides whether to retry.

Idempotency keys are **not** sent on the CLI's behalf. `POST /books`
is **not idempotent** on the API today; the SDK does not generate
`Idempotency-Key` headers.

Agents that want retry MUST:

1. Persist the request payload before invoking the CLI.
2. On a `kind: network | timeout | server | rateLimit` error, re-run
   the CLI with the saved payload.
3. Treat 409 (`kind: conflict`) as **never-retry**.

### Why

- Auto-retry hides bugs. A flaky network that masks an obvious
  configuration error is worse than a hard failure.
- Server-side idempotency requires server cooperation; the API does
  not yet accept an `Idempotency-Key` header on `POST /api/v1/books`,
  so client-side retry would create duplicates if the API ever does
  start honoring one (or if it silently retried internally).

### Acceptable-change rules

- **Adding** a `--max-retries N` flag (CLI-side retry, opt-in) is
  *non-breaking* so long as default behavior is unchanged.
- **Changing** the default to retry automatically is *breaking*.

### Open questions

- **§ 14.O-E**: When the API ships `Idempotency-Key` support on
  `POST /api/v1/books`, the CLI should generate one per invocation
  (UUIDv7) and emit it both as an outbound header and in the
  `--json` output (so the agent can persist it). Defer.

---

## 9. Configuration and auth

### Status

**Adopted.** Precedent: `studywise doctor` and the
`X-Studywise-Api-Key` transport feature (PR #45).

### Decision

Resolution order for the API base URL and API key:

1. `--base-url` / `--api-key` flags (per-invocation).
2. `STUDYWISE_API_BASE_URL` / `STUDYWISE_API_KEY` environment variables.
3. `~/.config/studywise/config.json` (`apiKey` and `apiBaseUrl` keys).

The CLI never reads anything else (no `.env`, no
`~/.netrc`, no OS keychain — yet, see § 14.O-F).

When `--api-key` is supplied, it overrides resolution #2 and #3 but the
resolved base URL still applies. The flag is hidden from `--help`
output by default (visible via `--help --show-hidden`) because the
expected path is the env var.

Auth scope: every command that hits the API requires `X-Studywise-Api-Key`.
There is no "anonymous" mode for write paths. Reads that the API marks
anonymous (e.g. `GET /api/v1/books/{id}`) still send the header if a
key is configured; the server ignores it.

### Acceptable-change rules

- **Adding** a new resolution source is *non-breaking*.
- **Removing** a source or reordering is *breaking*.

### Open questions

- **§ 14.O-F**: macOS Keychain support (`security find-generic-password`)
  as a fourth resolution source. Defer to a separate ADR.

---

## 10. Testing contract

### Status

**Adopted.** Precedent: `docs/testing/testing-strategy.md`.

### Decision

Two layers, never three:

- **Unit** (`[Category("Unit")]`): pure in-process tests of CLI logic
  (handler, options parsing, output formatter). No HTTP.
- **Integration** (`[Category("Integration")]`): the command runs end
  to end against an in-process WireMock server bound to a loopback
  port. The WireMock server fakes the SDK's `HttpClient`. No real
  network call, ever.

There is no `E2E` layer in this repo. Tests that need the real API
belong in the API repo's own integration suite, or in the frontend
repo's E2E suite.

Every new command MUST add:

- ≥ 3 unit tests (success path, validation failure path, output
  formatter).
- ≥ 2 integration tests (success path against WireMock, one error
  path — usually 401 or 422).

### Acceptable-change rules

- **Adding** a test category (`LongRunning`, ...) is *non-breaking*.
- **Splitting** the Integration project into multiple assemblies is
  *non-breaking* if naming is preserved.

### Open questions

_None._

---

## 11. Documentation rules

### Status

**Adopted.** Decided on 2026-10-18.

### Decision

Every command gets a doc page:

```
docs/commands/<noun>-<verb>.md
```

Each page must contain, in this order:

1. Synopsis
2. One example invocations (success and one failure each)
3. Flags (table: name, type, required, default, description)
4. Output shape (success and failure; includes the exact `--json`
   record shape)
5. Exit codes (subset of § 5.3 used by this command)
6. Cross-references (any plan doc, this design doc, SDK client method)

Adding or editing a command PR without its `docs/commands/...` page
fails CI.

`README.md`'s "Commands" section is generated from the
`docs/commands/` directory by a small doc-sync step in CI; the README
is not edited by hand for command enumeration.

### Acceptable-change rules

- **Adding** a new `docs/commands/<noun>-<verb>.md` page is
  *non-breaking*.
- **Renaming** a page is *breaking* (depends on back-compat in the
  README generator for one minor release).

### Open questions

_None._

---

## 12. Acceptable-change rules (master)

This section is the master list of acceptable-change rules across all
sections. Individual sections restate their rules for locality; this
section aggregates them as a single checklist for reviewers.

A change is *breaking* if **any** of the following holds:

- Renames a command, a noun, a verb, a flag, a field, an `error.kind`,
  a JSON property name, or an env var.
- Removes a noun, a verb, a flag, an `error.kind`, a JSON property,
  an env var, or a documented fallback.
- Changes a flag's *type* (string ↔ int ↔ bool ↔ array).
- Changes a JSON property's *type* (number ↔ string ↔ object).
- Changes an exit code.
- Changes the success-channel (stdout ↔ stderr) or the error-channel.
- Tightens validation (rejects what was previously accepted).
- Bumps a major SDK version (per § 7).
- Re-introduces an HTTP wrapper (`IStudywiseTransport` or
  equivalent).
- Changes a default to retry automatically.

Everything else is *non-breaking*.

**Breaking changes require an ADR entry in § 13.** Each ADR has a
date, an author, a one-paragraph rationale, and a migration note
(what to do if you relied on the previous shape).

---

## 13. Decision log

| # | Date       | Decision                                                                                                                       | ADR author |
|---|------------|--------------------------------------------------------------------------------------------------------------------------------|------------|
| 1 | 2026-10-18 | Adopt this doc as the CLI design record.                                                                                       | —          |
| 2 | 2026-10-18 | Command tree shape: `<noun> <verb> --flags` (singular noun, mandatory verb). `doctor` is the only exception.                    | —          |
| 3 | 2026-10-18 | Inputs are flags only; no prompts under any circumstance.                                                                      | —          |
| 4 | 2026-10-18 | Output contract: stdout for success, stderr for errors; `--json` emits a CLI-normalized record.                                 | —          |
| 5 | 2026-10-18 | Error contract: human line + structured envelope + exit-code map (0, 1, 2; 64–78 reserved).                                     | —          |
| 6 | 2026-10-18 | Adopt `YellowHouseStudio.Studywise.Sdk` as the sole HTTP abstraction; remove `IStudywiseTransport`.                             | —          |
| 7 | 2026-10-18 | No client-side auto-retry; no client-side idempotency keys. Agents persist payloads and retry themselves.                       | —          |
| 8 | 2026-10-18 | Per-command docs in `docs/commands/<noun>-<verb>.md`; README commands section is generated.                                     | —          |

---

## 14. Open questions

Items here are tracked but not blocking. They become ADRs when
decided.

- **§ 14.O-A** — Repeatable/nested flag syntax (e.g. `--author` for
  `CreateBookRequest.authors[]`). Decide before the first command
  that needs nested input ships.
- **§ 14.O-B** — Multi-format output (`--format csv|table|json`).
  Defer until asked.
- **§ 14.O-C** — `retryable` boolean on the structured error
  envelope. Defer until the SDK ships `Retry-After` support.
- **§ 14.O-D** — Wiring SDK resilience pipeline + CLI flags when
  the SDK ships it.
- **§ 14.O-E** — Client-side `Idempotency-Key` generation when the
  API starts honoring the header on `POST /api/v1/books`.
- **§ 14.O-F** — macOS Keychain as a fourth resolution source.

---

## 15. Cross-repo coupling

Three cross-repo facts this doc assumes. If any of them change, this
doc is wrong until updated.

1. **The SDK exists.** `YellowHouseStudio.Studywise.Sdk` is published
   from `studywise-api/src/Studywise.Contracts/` and consumed here via
   `Directory.Packages.props`. If the SDK is renamed, deprecated, or
   its boundaries change (it starts shipping server-side types), § 7
   is invalidated.
2. **The CLI does not own wire types.** Every type the CLI sends to
   or receives from the API lives in the SDK. Inlining a DTO into the
   CLI is a § 7 violation.
3. **`POST /api/v1/books` carries no education-level field.** The
   in-flight issue-26 plan (`docs/plans/issue-26-education-level-list.md`)
   collects education levels for a different command. If a future API
   revision adds `EducationLevelId` to `CreateBookRequest`, § 4.2's
   `CreateBookResult` must grow the field in lockstep.
