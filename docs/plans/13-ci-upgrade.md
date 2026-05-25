# Issue #13 Plan - CI Upgrade (align with studywise-api)

## Summary
Upgrade `studywise-cli` CI so pull requests and post-merge release behavior follow the same gate model as `studywise-api`: fast PR validation, approval-gated full validation, concrete coverage/doc-comment policy, and explicit GitHub protection settings.

## User Story
Som utvecklare
Vill jag att `studywise-cli` CI matchar `studywise-api` kvalitet och kontroller
Sa att PR:er far ratt kvalitetssakring innan merge

## Scope (This Issue)
- Define and implement CI entry points for PR fast checks, PR approval checks, production release, and coverage baseline update.
- Add and/or align docs-only skip behavior where intended.
- Ensure integration tests run in fast/full, and E2E runs in full.
- Enforce warnings-as-errors, analyzer/doc-comment policy, dependency vulnerability scan, and coverage policy.
- Define exact `main` branch protection and auto-merge success conditions.
- Define runtime verification scenarios with expected workflow/check outcomes and evidence location in issue #13.

## Out of Scope
- Re-architecting package/release versioning strategy beyond current production workflow intent.
- Major analyzer-rule refactors outside the selected doc-comment policy.
- Net-new deployment environments.

## CI Entry-Point Inventory (Required)

Every CI or CI-adjacent entry point touched by this issue is listed below with its required gates/properties.

| Entry point | Path | Trigger | Required gate/properties in this issue |
| --- | --- | --- | --- |
| Fast PR CI | `.github/workflows/ci-fast.yml` | `pull_request` (`opened`, `synchronize`, `reopened`) + `workflow_dispatch` | Must run on PR updates to `main`; must perform docs-only detection; must run build with warnings-as-errors; must run unit + integration tests; must finish green for docs-only PRs via explicit skip path (no pending required checks). |
| Full approval CI | `.github/workflows/ci-full.yml` | `pull_request_review` (`submitted`) + `workflow_dispatch` | Must execute only when review state is `approved` (or manual dispatch); must target PRs to `main`; must include docs-only detection, analyzer/doc-comment gate, vulnerability scan, unit + integration + E2E tests, and coverage gate; must publish `coverage-summary` artifact for baseline workflow. |
| Production CI | `.github/workflows/ci-production.yml` | `push` to `main` (with `paths-ignore`) + `workflow_dispatch` | Must own post-merge build/package/release behavior; must not duplicate approval-gated full suite as merge blocker; docs-only pushes should skip via `paths-ignore`. |
| Coverage baseline updater | `.github/workflows/update-baseline-on-merge.yml` | `pull_request.closed` on `main` (merged) + `workflow_dispatch` | Must resolve associated `CI Full` run, download `coverage-summary` artifact, update `.github/coverage-baseline.json`, and commit only when baseline changes. |
| Local PR mirror script | `scripts/ci-pr-local.ps1` (planned path) | Manual local developer invocation | Must mirror PR quality flow intent: restore/build warnings-as-errors, unit + integration tests always, optional E2E mode for parity checks before requesting approval. Script is part of developer/PR workflow inventory even if created in a follow-up implementation step. |

## Explicit Policy Decisions (Resolved in Plan)

### Documentation-comment policy
- Decision: use incremental policy now (not strict global `CS1591` in this issue).
- Enforced gate: `ci-full.yml` runs analyzer/doc-comment validation through existing analyzer configuration (`dotnet format analyzers --verify-no-changes`).
- Follow-up (separate issue): evaluate turning on strict `CS1591` once baseline cleanup is complete.

### Coverage policy
- Decision: enforce non-zero floor plus baseline-driven non-regression in full CI.
- For this issue's implementation target:
  - `ci-full.yml` must fail if coverage artifact is missing/invalid.
  - Minimum floor remains non-zero (`>= 1%`) to prevent null coverage passes.
  - Effective threshold is `max(configured floor, baseline minimum from .github/coverage-baseline.json)` when baseline enforcement is wired in.
- `update-baseline-on-merge.yml` remains the mechanism to refresh baseline after merged PRs using `coverage-summary` artifact from `CI Full`.

## Workflow Design and Responsibilities

### Fast vs full split
- `CI Fast`: required quick merge gate on every PR update.
- `CI Full`: required approval-gated merge gate on PR approval.
- `CI Production`: post-merge release path only.

### `ci-fast.yml` expectations
- Docs-only detection for `*.md`, `docs/**`, `*.txt`, and repo skill-doc paths already treated as non-code.
- Build with warnings-as-errors.
- Unit and integration tests.
- E2E, vulnerability scan, and coverage threshold enforcement stay out of fast lane.

### `ci-full.yml` expectations
- Triggered by review submission, runs only when state is `approved` and base branch is `main` (or manual dispatch).
- Includes docs-only detection and explicit skip success.
- Includes: warnings-as-errors build, analyzer/doc-comment gate, dependency vulnerability scan, unit/integration/E2E tests, coverage enforcement, and artifact upload (`coverage-summary`, failure test artifacts).

### `ci-production.yml` expectations
- Triggered after merge on `main` push (excluding docs-only via `paths-ignore`).
- Handles build/package/release publication.
- Does not act as required PR gate.

### `update-baseline-on-merge.yml` expectations
- Runs only for merged PRs (or manual dispatch).
- Locates matching `CI Full` run, pulls `coverage-summary`, updates `.github/coverage-baseline.json`, commits only when changed.

## GitHub Configuration Targets (Replace high-level verification)

These are the exact target settings to verify in GitHub repository settings.

### Branch protection rule for `main`
- Require a pull request before merging: enabled.
- Required approvals: minimum 1.
- Dismiss stale approvals when new commits are pushed: enabled.
- Require review from code owners: keep current repo default (do not change in this issue unless already required).
- Require conversation resolution before merging: enabled.
- Require status checks to pass before merging: enabled.
- Required checks list (exact):
  - `CI Fast / build-and-test`
  - `CI Full / Build and quality checks`
- Require branches to be up to date before merging: enabled.
- Include administrators: enabled (if already org standard; otherwise document deviation in issue evidence).
- Restrict who can push to `main`: no direct pushes except existing approved automation/bot rules.

### Auto-merge success conditions
- Repository-level auto-merge feature: enabled.
- PR can be set to auto-merge only when all are true:
  - At least one approval exists and is not stale.
  - `CI Fast / build-and-test` is successful for latest head SHA.
  - `CI Full / Build and quality checks` is successful for latest head SHA.
  - No required conversation unresolved.
  - Branch is up to date with `main` if required by protection rule.

## Runtime Verification Scenarios (Concrete)

Evidence for each scenario must be recorded in issue `#13` under a dedicated checklist comment titled `CI Upgrade Verification Evidence` with:
- PR link
- Commit SHA
- Screenshot or copied status-check list
- Pass/skip result notes per expected checks

| Scenario | Sample PR content | Expected workflows | Expected check results |
| --- | --- | --- | --- |
| A. Docs-only PR | Change only `docs/**` or `*.md` | `CI Fast` on PR update; `CI Full` after approval | `CI Fast / build-and-test` = success with docs-only skip message; `CI Full / Build and quality checks` = success with docs-only skip after approval; no E2E execution. |
| B. Code PR (no E2E impact) | Modify CLI source + unit/integration tests | `CI Fast` on push; `CI Full` after approval | Fast runs build + unit + integration and passes; full runs analyzer, vulnerability, unit, integration, E2E, coverage and passes. |
| C. Code PR with intentional vulnerability failure test | Temporary PR branch introducing known vulnerable package (test-only validation branch) | `CI Full` after approval | Vulnerability step fails with explicit vulnerable package output; PR not mergeable until fixed/reverted. Evidence captured then branch closed. |
| D. Coverage regression test | PR that drops coverage below baseline/floor (controlled test) | `CI Full` after approval | Coverage gate fails with reported actual vs required threshold; merge blocked; fix PR shows pass. |
| E. Post-merge baseline update | Merge passing PR from scenario B | `Update coverage baseline` on merged PR close | Workflow finds `CI Full` run, downloads `coverage-summary`, updates `.github/coverage-baseline.json` only if value changed, commits via bot. |
| F. Production release path | Merge non-doc code PR to `main` | `CI Production` on push | Production workflow runs build/package/release steps; docs-only merge does not trigger due to `paths-ignore`. |

## Risks and Mitigations
- `CI Full` duplicate runs from repeated approvals -> keep strict approval-state condition and concurrency cancellation.
- Docs-only required-check deadlock -> skip paths must produce successful completed checks, never pending.
- E2E flakiness from Dev Proxy -> explicit setup and failure artifact retention.
- Coverage noise/regressions -> baseline mechanism + clear threshold output in logs and issue evidence.

## Implementation Steps
1. Update/confirm `ci-fast.yml` docs-only + warnings-as-errors + unit/integration behavior.
2. Update/confirm `ci-full.yml` approval gate, quality checks, artifact outputs, and coverage enforcement policy.
3. Update/confirm `ci-production.yml` post-merge-only responsibility.
4. Update/confirm `update-baseline-on-merge.yml` artifact consumption and baseline commit behavior.
5. Add `scripts/ci-pr-local.ps1` in implementation (or tracked follow-up) to mirror PR-local quality flow.
6. Apply branch protection and auto-merge settings exactly as listed above.
7. Execute scenarios A-F and record evidence in issue #13 comment checklist.

## Definition of Done
- CI entry-point inventory exists in this plan and includes all workflow files plus `scripts/ci-pr-local.ps1` path.
- Plan explicitly resolves doc-comment and coverage policy decisions (no open policy decision left for implementation).
- Branch protection and auto-merge targets are explicitly defined (not generic bullets).
- Runtime verification scenarios define sample PR type, expected check names, pass/skip behavior, and evidence location.
