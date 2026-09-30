# Plan: smoke tests + deployment verification for the CLI

**Issue:** none (no GitHub issue tracked yet — file this when implementation starts)
**Branch:** `chore/smoke-and-deploy-verify` (new, separate from the
NUnit migration PR so the migration PR stays reviewable on its own)
**Scope:** 2 new files, 2 small edits, 1 doc edit.

## Why

The org test strategy (`docs/test_strategy.md` in the CodeStandards repo)
defines three deployment-related concepts. The CLI repo currently has
zero coverage for two of them and a partial overlap with the third:

| Concept | Definition (org doc) | CLI repo status |
|---|---|---|
| **Deployment Verification** | Quick check that the deployed service is up — `GET /health` + DB connectivity + auth-service reachable. Script, not a test layer. Runs **before** Smoke. | **Missing.** No script. |
| **Smoke** | "Same test cases as Fast, but runs against **real staging** after deploy." Failure → rollback. CI/CD step, not a test layer. | **Missing.** No workflow. |
| **E2E** | Critical user flows. Lives in the **frontend repo** (Playwright). Nightly or per-release. | **N/A — CLI doesn't have user flows.** Documented as out of scope below. |

After the NUnit migration (see `docs/plans/40-nunit-migration.md`),
the CLI repo has Unit + Integration +
Category=Unit + Category=Integration but no:

1. `[Category("Fast")]` markers — the org doc says "All Fast tests must pass before merge." We currently have no Fast identification. Adds the marker to the existing integration suite (the marker is documentation today; no filter uses it yet).
2. Deployment verification script — the org doc says it gates Smoke.
3. Smoke workflow — the org doc says it gates promotion.

This plan adds the three missing pieces. E2E is **deliberately out of
scope** — per the org doc, E2E is a frontend concern and the CLI repo
should not grow an E2E layer. See the "E2E explicitly out of scope"
section for the reasoning.

## Definitions (so we're aligned)

These three terms sound similar but mean distinct things. Pinning the
meanings here so the implementation matches the org doc exactly.

### Smoke

Smoke is **the released CLI binary, exercised against the staging
API, asserting it works end-to-end in a production-like environment.**
For the CLI, this means:

- Download the release artifact (`studywise-linux-x64.zip`).
- Extract the binary.
- Run `studywise doctor --json` against the staging API.
- Parse the JSON: assert `isSuccess: true` and that all three checks
  (`config`, `api-key`, `connection`) ran.
- Assert exit code 0.
- Pass = promotion. Fail = rollback.

This is **not** the Fast test suite retargeted at staging — see "The
smoke shape" below for why that doesn't translate to a CLI binary.
Smoke **does** spawn a new CLI process (it's the whole point — we
want to validate the released binary, not the test code).

### Deployment verification

Deployment verification is **a quick pre-Smoke check** that the
deployable actually works at all — before paying the cost of the
full smoke run. For the CLI:

- Run the released binary's `studywise doctor --check connection`
  against the staging API.
- Assert exit 0 and `connection: PASS`.
- Fail fast — don't proceed to Smoke if the binary can't even reach
  the API.

### E2E

Out of scope. The CLI has no user flow (Robert/Lilly is the primary
caller; there's no interactive UI). See "E2E explicitly out of scope"
below for the full reasoning.

## What goes away

Nothing. All current files stay.

## What gets added

| File | What |
|---|---|
| `scripts/deploy-verify.sh` | New. Exits 0 if the binary at `$1` can reach `/health` on the API at `$2`. Calls `studywise doctor --check connection` and greps for the success marker. |
| `.github/workflows/smoke.yml` | New. Triggered by `workflow_dispatch` and by `workflow_run` of `ci-production.yml` (success only). Downloads the release asset, runs `deploy-verify.sh` + `<binary> doctor --json` against staging. See "Smoke shape" below for the full flow. |
| `docs/testing/testing-strategy.md` | Edit. New section "Smoke + Deployment Verification" explaining the two new layers, how they relate to Fast, and why E2E is not in this repo. |

## What gets edited

| File | Change |
|---|---|
| `test/Studywise.CLI.IntegrationTests/.../DoctorCommandIntegrationTests.cs` | Add `[Category("Fast")]` on the class. All existing tests qualify — the Fast category is "subset of Integration that runs on every commit/PR" per the org doc, and these tests already run on PR. |
| `.github/workflows/ci-fast.yml` | Comment-only update: mention that `--filter "Category=Fast"` would also work now that the marker exists, and that PR-time CI runs the Fast set explicitly. |

## What this plan does NOT touch

- **E2E.** Out of scope. The frontend repo owns it. See "E2E
  explicitly out of scope" below.
- **The integration test code itself.** Smoke is a separate binary-
  spawning workflow (see "The smoke shape" below), not a reuse of the
  in-process Fast tests. The integration tests stay WireMock-based
  and unchanged.
- **The architecture doc.** No structural changes.

## The smoke shape — and why it's not "Fast tests retargeted at staging"

There's a real shape mismatch between the org strategy's definition of
Smoke ("the Fast test suite retargeted at staging") and the way our
CLI is built. Two reasons:

1. **Our integration tests can't be retargeted.** The
   `DoctorCommandIntegrationTests` build their own
   `WireMockServer.Start()` and set `STUDYWISE_API_BASE_URL` to the
   loopback URL **inside the test body**. The test never reads the
   env var from the surrounding process. Reusing these as smoke
   would require either (a) refactoring every test to read the env
   var, or (b) duplicating the assertions. Both are bigger changes
   than this plan warrants.

2. **The CLI surface is too small for Fast-as-Smoke to add value.**
   Fast tests in the org doc are "subset of Integration that runs on
   every commit/PR, < 5 min." For the CLI, all integration tests are
   already <5s (WireMock is in-process). Splitting them into Fast vs.
   LongRunning doesn't change what runs in CI today — the entire
   integration suite is already Fast.

**Pragmatic resolution:** Smoke for the CLI is **a single
binary-spawning check**, not a test suite. The smoke workflow:

1. Downloads the release artifact (`studywise-linux-x64.zip`).
2. Extracts the binary to a temp dir.
3. Runs `deploy-verify.sh <binary>` to confirm the binary launches
   and reaches the staging API.
4. Runs `<binary> doctor --json` against staging.
5. Parses the JSON: asserts `isSuccess: true` and that all three
   checks (`config`, `api-key`, `connection`) ran.
6. Asserts exit code 0.

This is "did the released binary work against the staging API we just
released it to" — which is the spirit of the org doc's Smoke without
the Fast-test reuse shape. The integration tests stay in-process with
WireMock; smoke is its own thing.

## Resolved design questions

1. **Where does the staged CLI binary live?** **Resolved: GitHub
   Actions itself.** The `ci-production.yml` workflow already builds
   and publishes `studywise-linux-x64.zip` as a GitHub Release asset.
   The smoke workflow can:
   - Download that release asset
   - Extract it
   - Run `deploy-verify.sh <path/to/studywise>` against a staging API
     URL stored as a repo variable
   - Run `studywise doctor --json` against staging and parse the
     output

   No persistent staging host needed — the binary lives in the
   release asset, the staging API URL is a repo variable
   (`STAGING_API_BASE_URL`), and the workflow runs on
   `ubuntu-latest`. This is the lowest-infra option that still
   validates "the released binary works against the staging API."

   Note: a self-hosted runner would let the binary persist between
   runs (avoiding the download step) but is new org infrastructure
   not present in Studywise-Api or SparkProgress today. Out of scope.

2. **Does the staging API require auth?** **Resolved:** `studywise
   doctor --check connection` doesn't need an api-key (it only hits
   `/health`, which is unauthenticated). For the full `--check all`
   smoke run, the workflow writes a fake
   `~/.config/studywise/config.json` with a staging api-key from a
   new repo secret `STUDYWISE_SMOKE_KEY`. This is separate from
   `STUDYWISE_API_KEY` (which is documented as unused after the NUnit migration).

3. **Who triggers Smoke?** **Resolved:** `workflow_dispatch` (manual
   re-run) + `workflow_run` on `ci-production.yml` success (auto on
   every release). Mirrors the api repo's auto-trigger pattern.

## E2E explicitly out of scope

After re-discussion: the CLI has no real "user flow" to E2E. The
binary has one meaningful entry point (`studywise doctor --json`),
the planned `words/progress/practice` commands are stubs, and the
intended invocation pattern (Robert/Lilly agent calls) isn't a user
flow. Smoke = binary-spawned `doctor --json` against staging is
enough end-to-end coverage for the CLI's current surface area.

A future iteration could add E2E for `words list` → `progress
show` → `practice start` chaining if those commands ever ship in a
form that benefits from full-stack verification. Flagged as
out-of-scope here.

## File-by-file change list

- [ ] `docs/plans/41-smoke-and-deploy-verify.md` — this file
- [ ] `scripts/deploy-verify.sh` — new, ~30 lines
- [ ] `.github/workflows/smoke.yml` — new, ~80 lines
- [ ] `test/Studywise.CLI.IntegrationTests/.../DoctorCommandIntegrationTests.cs` — add `[Category("Fast")]` on the class
- [ ] `.github/workflows/ci-fast.yml` — comment-only update
- [ ] `docs/testing/testing-strategy.md` — new "Smoke + Deployment Verification" section

## Out of scope (deliberately)

- E2E (CLI surface area is too small to benefit; flagged for future
  iteration if commands ship that need full-stack verification)
- Self-hosted runners (new org infrastructure; not in Studywise-Api
  or SparkProgress today)
- Refactoring integration tests to be env-var-driven instead of
  WireMock-driven (would let Smoke reuse Fast tests, but is a bigger
  change than this plan warrants; flag as a possible follow-up)
- Adding `[Category("LongRunning")]` markers (no LongRunning tests
  exist)
- Smoke results dashboard / notification routing

## Verification plan

- `deploy-verify.sh` smoke-tested locally against a known-bad URL
  (expect non-zero exit) and a known-good URL (expect zero exit).
- `smoke.yml` smoke-tested via `workflow_dispatch` against the dev's
  local machine. First iteration can use a publicly reachable URL
  (e.g. `https://api.studywise.io/health`) to validate the workflow
  plumbing even before staging exists.
- After merge: dispatch the smoke workflow against staging and
  confirm the doctor checks pass.

## Risks

- **Smoke scope creep.** It's tempting to add more smoke checks
  (`--help`, `--version`, command-flag parsing) once the binary is
  in the loop. Each new check needs a maintainer and a clear
  failure signal. Mitigation: keep smoke to `doctor --json` for
  now, add more only when there's a concrete need.
- **Auth drift** — if the staging api-key rotates, smoke fails until
  the secret is updated. Acceptable; this is a desired signal.
- **Staging availability** — if the staging API is down, every smoke
  run fails. Mitigation: the smoke workflow reports a clear "could
  not reach STAGING_API_BASE_URL" message so the failure isn't
  conflated with a CLI bug.
