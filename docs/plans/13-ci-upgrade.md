# Issue #13 Plan — CI Upgrade (align with studywise-api)

## Summary
Upgrade `studywise-cli` CI workflows to match the quality gate pattern used in `studywise-api`, including an approval-gated `ci-full.yml`, docs-only skip logic, deeper test coverage, and dependency vulnerability checks.

This plan keeps `ci-fast.yml` as the PR feedback loop and introduces `ci-full.yml` as the merge-quality gate triggered on PR approval.

---

## User Story
Som utvecklare  
Vill jag att `studywise-cli` CI matchar `studywise-api` kvalitet och kontroller  
Så att PR:er får rätt kvalitetssäkring innan merge

---

## Scope (This Issue)
- Add new `.github/workflows/ci-full.yml` triggered by PR review submission and gated on `approved`
- Add docs-only detection (`.md`, `docs/`, `.txt`) and skip expensive jobs when only docs change
- Add integration test execution to fast/full workflows where appropriate
- Add E2E test execution in full workflow (with Dev Proxy setup)
- Add dependency vulnerability check (`dotnet list ... --vulnerable --include-transitive`)
- Align required status checks so PR merge requires passing CI
- Ensure auto-merge can be used once required checks pass

---

## Out of Scope
- Re-architecting release/versioning strategy in `ci-release.yml`
- Adding deployment environments or cloud deployment in `studywise-cli` full CI
- Rewriting existing tests beyond what is needed for CI reliability

---

## Current State (Exploration)
- Existing workflows in `studywise-cli`:
  - `.github/workflows/ci-fast.yml`: build + unit tests on PR events
  - `.github/workflows/ci-release.yml`: build + tests + draft release on `main` push
- Missing workflow in `studywise-cli`:
  - `.github/workflows/ci-full.yml` (approval-gated)
- Reference in `studywise-api`:
  - `ci-fast.yml` includes docs-only detection and smoke-depth checks
  - `ci-full.yml` triggers on `pull_request_review` + manual dispatch, runs when review state is `approved`, includes vulnerability checks and broader test coverage

---

## Gap Analysis vs Issue Goals

1. Docs-only detection
   - Missing in `studywise-cli/ci-fast.yml`
   - Partially present in `ci-release.yml` via `paths-ignore`, but not equivalent behavior for PR workflows

2. Integration tests job
   - Missing in `ci-fast.yml`
   - Present in `ci-release.yml`

3. E2E tests job
   - Missing in all existing CLI workflows
   - Test project exists: `test/Studywise.CLI.E2ETests/Studywise.CLI.E2ETests.csproj`

4. Dependency vulnerability check
   - Missing in all existing CLI workflows

5. Approval-gated full CI
   - Missing: no `pull_request_review` approval workflow

6. Branch protection/required checks
   - Not defined in repo files; must be configured in GitHub branch protection rules

---

## Proposed Workflow Design

### 1) New `ci-full.yml` (approval gate)

Trigger and gate:
- `on.pull_request_review.types: [submitted]`
- `on.workflow_dispatch` for manual reruns
- Job-level `if`:
  - run when `github.event.review.state == 'approved'`
  - and PR targets `main`
  - or manual dispatch

Jobs:
- `docs-only-check`
  - checkout with `fetch-depth: 0`
  - diff `base.sha..head.sha`
  - set output `docs_only=true|false`
- `build-and-test` (needs docs-only-check)
  - skip quickly when docs-only
  - setup .NET + cache
  - restore + build
  - run unit tests
  - run integration tests
  - setup Dev Proxy
  - run E2E tests (`dotnet test test/Studywise.CLI.E2ETests/Studywise.CLI.E2ETests.csproj`)
  - run dependency vulnerability check and fail on vulnerable packages
  - upload test artifacts on failure

Notes for CLI-specific adaptation:
- Keep branch name `main` (not `master` as in API repo)
- No deploy-to-staging stage in full CI for CLI
- Keep runtime within practical timeout (e.g. 30-45 min)

### 2) Update `ci-fast.yml` (PR fast gate)

Changes:
- Add `docs-only-check` job and skip build/test on docs-only PRs
- Keep fast profile but add integration tests
- Keep E2E out of fast to preserve rapid feedback loop
- Optional: add lightweight vulnerability check in fast if runtime impact is acceptable; otherwise keep it full-only

Proposed fast execution:
- Restore
- Build
- Unit tests
- Integration tests

### 3) Review `ci-release.yml` alignment

Keep `ci-release.yml` focused on post-merge release flow, but align reliability:
- Ensure test project paths are correct and casing-safe:
  - current file references `Studywise.Cli.UnitTests.csproj` and `Studywise.Cli.IntegrationTests.csproj`
  - actual files are `Studywise.CLI.UnitTests.csproj` and `Studywise.CLI.IntegrationTests.csproj`
- Consider adding `.txt` to `paths-ignore` for consistency with docs-only policy
- Do not duplicate approval logic here (release is push-based)

---

## Docs-only Detection Rule

Recommended non-code-change pattern:
- Treat as docs-only when all changed files match one of:
  - `*.md`
  - `docs/**`
  - `*.txt`

Optional parity with API repo:
- Also treat `.cursor/skills/**`, `.opencode/skills/**`, `.codex/skills/**` as docs-only infra files if these paths are used in this repo.

---

## Acceptance Criteria Mapping

1. Add docs-only detection
   - `ci-fast.yml` and `ci-full.yml` include docs-only detection + skip behavior

2. Add integration tests job
   - `ci-fast.yml` runs integration tests
   - `ci-full.yml` runs integration tests

3. Add E2E tests job
   - `ci-full.yml` runs E2E tests with Dev Proxy available

4. Add dependency vulnerability check
   - `ci-full.yml` includes `dotnet list ... --vulnerable --include-transitive` and fails on findings

5. PR requires passing CI before merge
   - Branch protection configured to require `CI Fast` and `CI Full` checks on `main`

6. Auto-merge can be enabled
   - With required checks + approvals configured, auto-merge can be used in PR UI

---

## Risks and Mitigations

- Risk: `ci-full` runs multiple times for repeated approvals/comments
  - Mitigation: strict job `if` on `review.state == 'approved'` + concurrency cancel-in-progress

- Risk: E2E instability due to Dev Proxy availability
  - Mitigation: explicit Dev Proxy install/start step and clear failure output

- Risk: False failures in vulnerability check from incompatible projects
  - Mitigation: use same defensive parsing pattern as API workflow and fail only on real vulnerability findings

- Risk: Required check deadlock if docs-only PR skips jobs
  - Mitigation: docs-only job itself must succeed and gated jobs should no-op cleanly rather than remain pending

---

## Implementation Steps

1. Add `.github/workflows/ci-full.yml` modeled after API workflow but CLI-tailored.
2. Add docs-only detection + integration test stage to `.github/workflows/ci-fast.yml`.
3. Add vulnerability check to `ci-full.yml` (and optionally fast if desired).
4. Update `.github/workflows/ci-release.yml` path casing for test projects and align docs ignore patterns.
5. Validate workflows with dry-run logic review and one test PR.
6. Configure branch protection on `main` to require `CI Fast` and `CI Full`.
7. Validate auto-merge behavior on a sample PR after checks pass.

---

## Definition of Done

- `ci-full.yml` exists and runs on PR approved state (`pull_request_review.submitted` + approved gate).
- `ci-fast.yml` skips docs-only changes and includes integration tests.
- Full CI runs unit + integration + E2E + dependency vulnerability checks.
- `ci-release.yml` is aligned for correct test project paths.
- Branch protection requires passing CI before merge.
- Team can enable auto-merge on PRs when checks/approvals are satisfied.
