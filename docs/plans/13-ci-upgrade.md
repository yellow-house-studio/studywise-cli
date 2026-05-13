# Issue #13 Plan — CI Upgrade (align with studywise-api)

## Summary
Upgrade `studywise-cli` CI workflows to match the quality gate pattern used in `studywise-api`, including an approval-gated `ci-full.yml`, docs-only skip logic, explicit code quality gates (warnings/errors, documentation-comment policy, coverage), and dependency vulnerability checks.

This plan keeps `ci-fast.yml` as the fast PR feedback loop, introduces `ci-full.yml` as the approval gate, and aligns release/production workflow responsibilities.

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
- Add quality checks for warnings-as-errors, documentation comments policy, and code coverage threshold
- Align required status checks so PR merge requires passing CI
- Ensure auto-merge can be used once required checks pass
- Clarify `ci-production` responsibility vs `ci-release` and align workflow naming/behavior

---

## Out of Scope
- Re-architecting release/versioning strategy in `ci-release.yml`
- Adding deployment environments or cloud deployment in `studywise-cli` full CI
- Rewriting existing tests beyond what is needed for CI reliability
- Large refactor of project-wide analyzer/doc-comment rules beyond minimal CI enforcement

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

5. Code quality checks beyond tests
   - Warnings-as-errors policy is not explicit/consistent across workflows
   - No explicit documentation-comment quality gate
   - No coverage collection/threshold enforcement in CI

6. Approval-gated full CI
   - Missing: no `pull_request_review` approval workflow

7. Branch protection/required checks
   - Not defined in repo files; must be configured in GitHub branch protection rules

8. Production/release workflow semantics
   - Existing `ci-release.yml` mixes validation and release creation
   - No explicit `ci-production.yml` equivalent to API naming/pattern

---

## Proposed Workflow Design

### Quality Gate Split (Fast vs Full)

- `ci-fast.yml` (required on PR updates):
  - compile/build with warnings as errors
  - unit + integration tests
  - docs-only skip
  - no E2E, no coverage threshold enforcement
- `ci-full.yml` (required on PR approval):
  - everything in fast, plus E2E, vulnerability scan, coverage collection + threshold check, stricter doc-comment gate

Rationale: keep contributor feedback fast while placing heavier quality checks at approval gate.

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
  - restore + build with warnings as errors
  - run doc-comment quality check (see policy below)
  - run unit tests
  - run integration tests
  - setup Dev Proxy
  - run E2E tests (`dotnet test test/Studywise.CLI.E2ETests/Studywise.CLI.E2ETests.csproj`)
  - run dependency vulnerability check and fail on vulnerable packages
  - collect code coverage and enforce minimum threshold
  - upload test artifacts on failure

Notes for CLI-specific adaptation:
- Keep branch name `main` (not `master` as in API repo)
- No deploy-to-staging stage in full CI for CLI
- Keep runtime within practical timeout (e.g. 30-45 min)

Doc-comment policy options (decide in implementation PR):
- Option A (strict, preferred if codebase is ready): enforce missing XML docs as errors for public API (`CS1591`) in `ci-full`
- Option B (incremental): run analyzer/doc checks in `ci-full` and fail on configured doc rules only; add `CS1591` after baseline cleanup

Recommended default for this issue: Option B, then create follow-up hardening issue to turn on strict `CS1591` globally.

### 2) Update `ci-fast.yml` (PR fast gate)

Changes:
- Add `docs-only-check` job and skip build/test on docs-only PRs
- Keep fast profile but add integration tests
- Keep E2E out of fast to preserve rapid feedback loop
- Build with warnings as errors (fast signal for quality regressions)
- Keep vulnerability and coverage threshold checks in full (not fast)

Proposed fast execution:
- Restore
- Build (warnings as errors)
- Unit tests
- Integration tests

### 3) `ci-production.yml` vs `ci-release.yml` alignment

Decision:
- Add `ci-production.yml` in this issue to match API workflow naming and intent.
- Keep existing `ci-release.yml` temporarily, then deprecate/remove once `ci-production.yml` is validated.

Responsibility split:
- `ci-fast` + `ci-full`: quality validation and merge gates
- `ci-production`: post-merge publish/release only (no full test suite duplication)

`ci-production.yml` should:
- trigger on `push` to `main` (and optionally tags)
- skip docs-only changes via `paths-ignore` including `*.md`, `docs/**`, `*.txt`
- build/package/publish release artifacts
- create/update release draft or publish release (team choice)

`ci-production.yml` should not:
- rerun full quality suite already required pre-merge
- duplicate approval-gated checks

Note on "does it publish updates":
- In this issue, goal is to define production workflow structure and ensure it performs actual publish/release artifact update.
- If final production destination (for example package registry channel/versioning strategy) is undecided, keep publish target as draft GitHub Release in this issue and track "final production publish policy" as follow-up.

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

5. Add code quality checks
   - `ci-fast.yml` and `ci-full.yml` build with warnings-as-errors
   - `ci-full.yml` runs doc-comment/analyzer quality gate
   - `ci-full.yml` enforces code coverage threshold

6. PR requires passing CI before merge
   - Branch protection configured to require `CI Fast` and `CI Full` checks on `main`

7. Auto-merge can be enabled
   - With required checks + approvals configured, auto-merge can be used in PR UI

8. Production workflow alignment
   - `ci-production.yml` exists with publish/release responsibility and no duplicated full test gate

---

## Risks and Mitigations

- Risk: `ci-full` runs multiple times for repeated approvals/comments
  - Mitigation: strict job `if` on `review.state == 'approved'` + concurrency cancel-in-progress

- Risk: E2E instability due to Dev Proxy availability
  - Mitigation: explicit Dev Proxy install/start step and clear failure output

- Risk: False failures in vulnerability check from incompatible projects
  - Mitigation: use same defensive parsing pattern as API workflow and fail only on real vulnerability findings

- Risk: Coverage gate blocks PRs unexpectedly due to baseline mismatch
  - Mitigation: establish baseline file first, enforce "no regression" or agreed minimum threshold

- Risk: Doc-comment gate causes high initial noise
  - Mitigation: start with incremental policy in `ci-full`, then tighten in follow-up issue

- Risk: Required check deadlock if docs-only PR skips jobs
  - Mitigation: docs-only job itself must succeed and gated jobs should no-op cleanly rather than remain pending

---

## Implementation Steps

1. Add `.github/workflows/ci-full.yml` modeled after API workflow but CLI-tailored.
2. Add docs-only detection + integration tests + warnings-as-errors build to `.github/workflows/ci-fast.yml`.
3. Add vulnerability check, doc-comment/analyzer gate, and coverage threshold gate to `ci-full.yml`.
4. Add `.github/workflows/ci-production.yml` for post-merge publish/release responsibilities.
5. Deprecate or simplify `.github/workflows/ci-release.yml` to avoid duplicate quality checks.
6. Validate workflows with dry-run logic review and one test PR + one post-merge run.
7. Configure branch protection on `main` to require `CI Fast` and `CI Full`.
8. Validate auto-merge behavior on a sample PR after checks pass.

---

## Definition of Done

- `ci-full.yml` exists and runs on PR approved state (`pull_request_review.submitted` + approved gate).
- `ci-fast.yml` skips docs-only changes and includes integration tests + warnings-as-errors build.
- Full CI runs unit + integration + E2E + dependency vulnerability + doc-comment/analyzer + coverage-threshold checks.
- `ci-production.yml` owns post-merge release/publish flow; `ci-release.yml` is removed or reduced to avoid duplicated validation.
- Branch protection requires passing CI before merge.
- Team can enable auto-merge on PRs when checks/approvals are satisfied.
