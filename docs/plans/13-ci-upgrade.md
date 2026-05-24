# Issue #13 Plan - CI Upgrade (align with studywise-api)

## Summary
Most of the Issue #13 CI implementation is already in place. This plan updates the issue to reflect current reality, focuses on the true remaining gaps, and defines concrete verification for each acceptance criterion.

Primary remaining work is policy and configuration hardening:
- finalize a meaningful coverage threshold strategy
- finalize documentation-comment policy strictness
- verify and enforce GitHub branch protection/required checks and auto-merge behavior

---

## User Story
Som utvecklare  
Vill jag att `studywise-cli` CI matchar `studywise-api` kvalitet och kontroller  
Sa att PR:er far ratt kvalitetssakring innan merge

---

## Current State Snapshot (As Implemented)

### Existing workflow files
- `.github/workflows/ci-fast.yml`
- `.github/workflows/ci-full.yml`
- `.github/workflows/ci-production.yml`
- `.github/workflows/update-baseline-on-merge.yml`

### Already implemented
1. Docs-only detection in fast/full CI
   - Present in `ci-fast.yml` and `ci-full.yml` via `docs-only-check` jobs.

2. Integration tests in fast/full CI
   - Present in `ci-fast.yml` and `ci-full.yml`.

3. E2E tests in full CI
   - Present in `ci-full.yml`, including Dev Proxy setup and E2E test execution.

4. Approval-gated full CI
   - Present in `ci-full.yml` (`pull_request_review` + approved-state gate + `main` target check).

5. Dependency vulnerability scanning
   - Present in `ci-full.yml` using `dotnet list ... --vulnerable --include-transitive` with fail logic.

6. Warnings-as-errors build gate
   - Present in both fast and full workflows.

7. Production workflow split
   - `ci-production.yml` exists and handles post-merge build/package/release responsibilities.

8. Coverage artifact baseline flow
   - `update-baseline-on-merge.yml` exists and updates `.github/coverage-baseline.json` from `CI Full` artifacts.

---

## True Remaining Gaps

1. Coverage threshold policy is still effectively placeholder
   - `ci-full.yml` uses `MIN_COVERAGE_PERCENT: 1`.
   - This enforces a technical gate, but not a meaningful quality threshold.

2. Documentation-comment policy is not finalized
   - Analyzer gate exists (`dotnet format analyzers --verify-no-changes`), but explicit policy decision is not locked:
     - incremental analyzer-only policy, or
     - strict missing XML-doc enforcement (`CS1591`) for public API.

3. GitHub settings are external and must be verified
   - Required checks and approval rules are not managed in repo files.
   - Need confirmation in branch protection that `CI Fast` and `CI Full` are required on `main`.
   - Need confirmation that auto-merge works once requirements are met.

4. End-to-end behavior validation still needed
   - Need live PR validation for docs-only and code-change paths to ensure required checks resolve as expected.

---

## Acceptance Criteria Status + Verification

1. Add docs-only detection
   - Status: Done in workflow code.
   - Evidence: `ci-fast.yml` + `ci-full.yml` `docs-only-check` jobs.
   - Verify:
     1) Open docs-only PR (for example update `README.md`).
     2) Confirm both CI workflows complete successfully with docs-only skip behavior.
     3) Confirm required checks show success (not pending indefinitely).

2. Add integration tests job
   - Status: Done in workflow code.
   - Evidence: integration test steps in `ci-fast.yml` and `ci-full.yml`.
   - Verify:
     1) Open code-change PR.
     2) Confirm integration test step executes in `CI Fast` and in approved `CI Full` run.

3. Add E2E tests job
   - Status: Done in workflow code.
   - Evidence: Dev Proxy setup + E2E test step in `ci-full.yml`.
   - Verify:
     1) Approve a code-change PR to trigger `CI Full`.
     2) Confirm Dev Proxy setup runs.
     3) Confirm E2E test project executes and reports result.

4. Add dependency vulnerability check
   - Status: Done in workflow code.
   - Evidence: vulnerability scan step in `ci-full.yml`.
   - Verify:
     1) Confirm step runs on approved PR.
     2) Optional hardening test: introduce a known vulnerable package in test branch and verify CI Full fails.

5. Add code quality checks
   - Status: Partial (implemented, policy not finalized).
   - Evidence:
     - warnings-as-errors build in fast/full
     - analyzer quality gate in full
     - coverage enforcement step in full
   - Remaining:
     - Set meaningful coverage threshold strategy (fixed minimum or baseline/no-regression).
     - Decide and document doc-comment policy strictness.
   - Verify:
     1) Add temporary warning in PR and confirm fast/full fail.
     2) Validate analyzer gate catches analyzer violations.
     3) Validate coverage gate fails when below agreed policy.

6. PR requires passing CI before merge
   - Status: Pending external verification.
   - Evidence needed: GitHub branch protection settings for `main`.
   - Verify:
     1) Confirm branch protection requires `CI Fast` and `CI Full`.
     2) Confirm PR cannot merge when either check fails/pending.

7. Auto-merge can be enabled
   - Status: Pending external verification.
   - Evidence needed: repo merge settings + branch protection compatibility.
   - Verify:
     1) Enable auto-merge on sample PR after approvals.
     2) Confirm merge occurs automatically once required checks pass.

8. Production workflow alignment
   - Status: Done in workflow code; runtime behavior should be validated.
   - Evidence: `ci-production.yml` exists and is post-merge focused.
   - Verify:
     1) Merge code PR to `main`.
     2) Confirm `CI Production` runs.
     3) Confirm release artifacts are created/published as expected.

---

## Remaining Work Plan

1. Coverage policy hardening
   - Define target approach:
     - Option A: fixed minimum percentage (for example 60%+)
     - Option B: no-regression against `.github/coverage-baseline.json`
   - Implement chosen policy in `ci-full.yml`.

2. Doc-comment policy decision
   - Choose policy:
     - Option A: keep incremental analyzer-based gate
     - Option B: add strict `CS1591` enforcement for public API
   - Document policy and any phased rollout.

3. GitHub settings verification/update
   - Confirm `main` branch protection requires `CI Fast` + `CI Full`.
   - Confirm approval requirements are aligned with issue intent.
   - Confirm auto-merge is enabled and functional.

4. Run end-to-end validation scenarios
   - docs-only PR
   - standard code-change PR with approval
   - post-merge production run

---

## Risks and Mitigations

- Risk: Coverage threshold chosen too aggressively and blocks active development.
  - Mitigation: start with agreed baseline/no-regression policy and tighten gradually.

- Risk: Strict doc-comment policy causes high initial noise.
  - Mitigation: phase policy with incremental gate first, strict mode in follow-up.

- Risk: Required checks appear green in workflow code but branch protection is misconfigured.
  - Mitigation: explicitly verify merge blocking behavior on sample PR.

- Risk: Docs-only skip logic interacts poorly with required checks.
  - Mitigation: validate docs-only PR scenario and ensure checks resolve to success.

---

## Closure Checklist

- [ ] Coverage policy is finalized and implemented (not placeholder `1%`).
- [ ] Doc-comment policy is finalized and documented (incremental or strict `CS1591`).
- [ ] Branch protection on `main` requires `CI Fast` and `CI Full`.
- [ ] Auto-merge verified working with approvals + required checks.
- [ ] Docs-only PR validation completed and recorded.
- [ ] Code-change approved PR validation completed and recorded.
- [ ] `CI Production` post-merge release artifact validation completed.
- [ ] Acceptance criteria 1-8 marked with evidence links in issue #13.

---

## Definition of Done

Issue #13 is done when:
- workflow implementation and settings verification both confirm required CI behavior,
- remaining policy gaps (coverage + doc-comments) are explicitly resolved,
- and each acceptance criterion has runtime verification evidence, not only workflow-file presence.
