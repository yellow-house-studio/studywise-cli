# Issue #32 Plan — Local CI Scripts + AGENTS.md for studywise-cli

## Summary

Port the local CI validation surface from `studywise-api/master` into
`studywise-cli`. Bring over `scripts/verify.sh` (unified entry point
that mirrors all CI workflows), `scripts/lib/test-report.sh` (TRX
summary helpers), `scripts/setup-env.sh` (tool bootstrap),
`scripts/ci-pre-push-hook.sh` + `install-ci-pre-push-hook.sh` (pre-push
gate), `.github/cspell.json` (source spell-check wordlist), and
`AGENTS.md` at repo root. Convert the coverage model from a single
`MIN_COVERAGE_PERCENT` env var to the per-assembly baseline format that
api uses, and update the four existing workflows
(`ci-fast.yml`, `ci-full.yml`, `ci-production.yml`,
`update-baseline-on-merge.yml`) to delegate to `verify.sh` so the
script is the single source of truth.

This is the "do the same thing as ci" piece the user asked for:
`verify.sh` exists to mirror the GitHub Actions workflows, so PRs that
pass locally don't fail remotely (and vice versa).

## Why

`studywise-cli` is fully aligned with `studywise-api` at the workflow
level (PR #14 / #13 closed that gap) but has **no local mirror**.
Three consequences:

1. Devs push code that passes locally via ad-hoc `dotnet test` but
   fails the PR gate on E2E setup, format drift, or coverage drop —
   the PR feedback loop is 5–15 min of runner minutes when it could be
   seconds.
2. There is no canonical pre-push gate. Other repos use a
   `verify.sh`-driven `.git/hooks/pre-push` to catch failures before
   the push; cli doesn't.
3. There is no `AGENTS.md` documenting the worktree convention
   (`main`, not `master`), commit policy, or what "done" means —
   agents working in this repo guess every time.

## Scope (this issue)

- **`scripts/verify.sh`** — unified entry point. Arg-parsing mirrors
  studywise-api. Phases: pre-flight → restore → build (Release,
  `TreatWarningsAsErrors`, `EnforceXmlDocs`) → format → analyzers →
  gitleaks → actionlint → cspell → security scan (vulnerable
  packages) → unit tests → integration tests → E2E tests (skip if
  `STUDYWISE_API_KEY` missing, like api does for Stripe) →
  coverage merge (ReportGenerator) → coverage gate (per-assembly
  drop ≤1pp, new-file ≥80%) → baseline auto-raise → PR comment
  (`--post-comment`).
- **`scripts/setup-env.sh`** — same tool list as api minus azurite and
  stripe; plus node (still needed for `npx cspell@10`) +
  reportgenerator + gitleaks + actionlint.
- **`scripts/lib/test-report.sh`** — port verbatim, except the
  `test_stages` list is `["Unit", "Integration", "E2E"]` (cli has 3
  test projects, not 4).
- **`scripts/ci-pre-push-hook.sh`** + **`install-ci-pre-push-hook.sh`** —
  identical to api; run `verify.sh --scope=fast` before push.
- **`scripts/README.md`** — port, adapted for cli's toolset (drop
  Stripe/Proton Pass references, add `STUDYWISE_API_KEY` E2E note).
- **`AGENTS.md`** — port from api, edit:
  - Default branch: `main`, not `master`.
  - Worktree path: `../studywise-cli-<NN>-<slug>` (worktrees go in
    `studywise-cli/`, sibling to `main/`, per the existing pattern).
  - Drop RavenDB, Auth0 M2M, Stripe live-key, and the
    `DEPLOY_FULL_VERIFY` release-deploy migration section — none of
    these apply to cli.
  - Test scope renamed: api has unit + application + integration +
    api; cli has unit + integration + e2e. The `--scope=integration`
    flag's definition adjusts to "unit + integration (no e2e)".
  - Keep the pre-push gate (still canonical), the docs-only carve-out,
    the plan-doc convention (`docs/plans/<slug>.md`), the squash-merge
    policy, and the three-tier boundaries.
- **`.github/cspell.json`** — port, prune api's wordlist (drop
  RavenDB/Auth0/Stripe-specific entries), keep csharp-language-id +
  `src/**/*.cs` glob. Add a `ignorePaths` for `bin/` / `obj/` / `TestResults/`
  so cspell doesn't lint generated code.
- **`.github/coverage-baseline.json`** — convert from the current
  `{"minimumCoverage": 1, ...}` shape to the per-assembly
  `{"assemblies": {<asm>: {covered, valid}}, ...}` shape that
  `verify.sh` Phase 8 + 9 read. Seed the assemblies map by running
  `verify.sh --scope=full` once and capturing the per-assembly
  numbers from the merged Cobertura.xml.
- **Workflow updates** — thin out `ci-fast.yml`, `ci-full.yml`,
  `ci-production.yml`, `update-baseline-on-merge.yml` so each runs
  the matching `verify.sh` scope instead of duplicating the dotnet
  command sequence. This makes `verify.sh` the source of truth —
  editing the local script and editing the workflow in tandem stops
  being necessary.

## Adapted for CLI (key differences from api)

| Concern | api | cli |
|---|---|---|
| Default branch | `master` | `main` |
| Solution | `Studywise.Server.sln` | `Studywise.Cli.sln` |
| Test projects | Unit, Application, Integration, Api (4) | Unit, Integration, E2E (3) |
| `--scope=integration` | Unit + Application + Integration (no Api) | Unit + Integration (no E2E) |
| E2E / API test prereq | `StripeApiKey` (Stripe) | `STUDYWISE_API_KEY` (Studywise API base URL) |
| E2E tooling | none | Dev Proxy (`dev-proxy-tools/actions/setup@v1`) for tests to mock health endpoint |
| Storage backend | RavenDB | none |
| Auth backend | Auth0 M2M | `STUDYWISE_API_KEY` (API key) |
| Local blob storage | Azurite | none |
| Source path for cspell | `src/**/*.cs` | `src/**/*.cs` (same — keep) |
| Coverage baseline | per-assembly JSON (already done) | per-assembly JSON (migrate from `minimumCoverage`) |

## Out of Scope

- Changing the test frameworks (xunit is fine for cli; api uses
  NUnit + FluentAssertions but cli's tests are already xunit).
- Adding new test projects or test categories (Category=Fast doesn't
  apply — xunit doesn't have an attribute-for-Category that maps to
  dotnet test --filter; we drop the `--scope=fast` filter and run
  everything).
- Refactoring the release / production workflow beyond the
  "delegate to verify.sh" change. The `softprops/action-gh-release`
  flow in `ci-production.yml` stays as-is.
- Replacing xunit with NUnit — out of scope; the test-report.sh
  parser handles both NUnit (api) and xunit (cli) TRX format because
  it parses the standard VS TRX schema, not the framework output.
- A standalone ci-artifacts archive step in `ci-dev` — api has
  `deploy-verify.yml` archive `TestResults/Verify/` (2-day on fail,
  30-day on pass); we don't need that here, the upload-on-failure in
  `ci-full.yml` is sufficient for cli.
- A separate `DEPLOY_FULL_VERIFY` migration flip (cli's `ci-production.yml`
  is a thin post-merge publish, not a deploy gate). The variable
  doesn't apply.

## Test approach

Run `verify.sh` end-to-end on the worktree in three configs:

1. `./scripts/verify.sh --scope=unit --no-fail-fast` — fastest path;
   validates restore + build + format + analyzers + unit tests +
   coverage merge. Confirms every phase finds its inputs and reports
   cleanly.
2. `./scripts/verify.sh --scope=full --skip-e2e` — full suite minus
   E2E (which needs Dev Proxy + `STUDYWISE_API_KEY`).
3. `./scripts/verify.sh --scope=integration` — unit + integration.

If a phase fails, fix the script (or the seed baseline) and re-run
until clean. The final commit before opening the PR should run #2
cleanly on the worktree; #1 and #3 are sanity checks during
implementation.

After the scripts are in, run the workflows once on a draft PR
(via `gh workflow run` for any dispatch-triggered one) to confirm the
CI side picks up the verify.sh-delegated changes. The pre-push hook
(`install-ci-pre-push-hook.sh`) is installed locally but NOT
checked into the repo (`.git/hooks/` is not committed).

## Acceptance criteria

- `AGENTS.md` checked in at repo root; documents `main`, worktree
  naming, `verify.sh` as canonical entry point, pre-push hook,
  commit/squash policy, and the three-tier boundaries.
- `scripts/verify.sh` checked in, executable, with phases and arg
  parsing that match api.
- `scripts/setup-env.sh`, `scripts/lib/test-report.sh`,
  `scripts/ci-pre-push-hook.sh`, `scripts/install-ci-pre-push-hook.sh`,
  `scripts/README.md` all checked in.
- `.github/cspell.json` checked in, adapted for cli's source tree.
- `.github/coverage-baseline.json` migrated to per-assembly format
  and seeded from a real `verify.sh --scope=full` run.
- Workflows (`ci-fast.yml`, `ci-full.yml`, `ci-production.yml`,
  `update-baseline-on-merge.yml`) delegate to `verify.sh` instead of
  duplicating dotnet command sequences.
- `verify.sh --scope=unit`, `--scope=integration`, `--scope=full`
  pass on a clean worktree.
- AGENTS.md §"What done means" points at `verify.sh` as the local CI
  surface and `--scope=full` as the heavy check.

## Definition of Done

- PR open, linked to issue #32.
- All checkboxes above are ticked.
- Plan doc updated (if deviations from this plan) before merge.
- A follow-up issue exists if the `STUDYWISE_API_KEY` skip-message
  wording or the baseline-seed threshold needs adjustment.

## Blocked-by

- None. Independent of any open api work.

## Risks + mitigations

- **Risk**: cspell finds a bunch of words in cli's source on first
  run. **Mitigation**: seed the wordlist generously from cli's
  existing source (read every `src/**/*.cs`, harvest identifiers
  the existing build doesn't flag). Add a `.cspell-ignore` comment
  for domain terms we know are correct (e.g. `Studywise`,
  `YellowHouseStudio`).
- **Risk**: Coverage baseline seed drops below the previous
  `minimumCoverage: 1` floor because the per-assembly parse is
  strict about `covered` vs `valid`. **Mitigation**: seed from a
  real `--scope=full` run; the per-file entries that come out of
  the Cobertura merge are the source of truth, not the 1% placeholder
  (which was always a "tests pass" floor, not a real coverage
  measurement).
- **Risk**: Existing `ci-production.yml`'s `paths-ignore` misses
  paths the workflow now skips. **Mitigation**: keep the
  `paths-ignore` block; add `scripts/**` and `.github/coverage-baseline.json`
  if needed.
- **Risk**: PR diff balloons because local main was stale. **Mitigation**:
  worktree was created AFTER `git fetch && git reset --hard origin/main`
  — verified `git rev-parse HEAD` matches `origin/main` (`178ec7f`).