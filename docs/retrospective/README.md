# Retrospectives

Write-ups capturing corrections, lessons, and cross-cutting implications
from closed work. Both modes are opened as a follow-up PR **after** the
work PR lands (see AGENTS.md §"Self-Improvement Loop").

| Mode | File pattern | Trigger | Scope |
|---|---|---|---|
| **Issue retrospective** | `issue-<N>-<slug>.md` | Any merged PR that closed a GitHub issue AND had user corrections during the work | One issue. Corrections only. |
| **Feature retrospective** | `feature-<slug>.md` | A feature warrants a fuller postmortem | One feature. Broader scope. |

## Convention

The **full convention — including the file templates, the "Pattern / The
mistake / The rule" correction structure, and the "every correction needs a
tracked action" rule — is maintained in
[studywise-api/docs/retrospective/README.md](https://github.com/yellow-house-studio/studywise-api/blob/master/docs/retrospective/README.md).
This file is the CLI repo's index and the pointer to it; the templates are
not duplicated.

What carries over unchanged:

- **Naming:** `issue-<N>-<slug>.md` (N is the GitHub issue number) and
  `feature-<slug>.md`.
- **When:** a follow-up PR, after the work PR is merged. Never bundled into
  the work PR.
- **No editorializing:** capture what was corrected, not whether the
  correction felt good or bad.
- **Every correction needs a tracked action.** Each entry in "Actions taken"
  is either done in the retrospective PR (linked) or filed as a GitHub issue
  (linked). An unchecked `- [ ]` at merge time is a regression.
- **Update the index below at PR-creation time, not at merge time.**

## Why this folder exists

It did not, until 2026-10-22.

During the SDK adoption work a credential was exposed in tool output — the
same failure mode already captured in
[studywise-api's `issue-627-stripe-key-bootstrap` retrospective](https://github.com/yellow-house-studio/studywise-api/blob/master/docs/retrospective/issue-627-stripe-key-bootstrap.md).
That lesson was not reachable from this repo: there was no retrospective
index here to point at it, and the API repo's index was never opened before
starting work that involved the same class of secret.

The folder exists so the next reader in this repo can find the lesson
without knowing which other repo wrote it.

## Topical index

| Topic | Retrospective | Applies when |
|---|---|---|
| A 404 from a package feed is not evidence of absence | [feature-sdk-adoption](./feature-sdk-adoption.md) | Checking whether a package version exists on a private or authenticated feed (GitHub Packages, Azure Artifacts, a private NuGet source). Verify with an authenticated request to the version-index endpoint, and run the actual restore before asserting that something was never published. |
| A tool's exit code is not the answer — do the real operation | [feature-sdk-adoption](./feature-sdk-adoption.md) | Reporting that a merge, a build or a merge-order check is clean based on a dry-run or a tool's exit status. Perform the real merge in a real clone and read the result. |
| Another team's obligation to us is the surface we consume | [feature-sdk-adoption](./feature-sdk-adoption.md) | Filing an issue in another repo because their internal pipeline or tooling is broken. Their obligation to us is the API or SDK surface we consume; their internal machinery is theirs. |
| Lead with verified state, not a prediction | [feature-sdk-adoption](./feature-sdk-adoption.md) | Writing a bug report whose headline is what a tool *will* compute or produce. State the verified condition; the prediction belongs in a note, if at all. |
| Never print a credential-bearing config into tool output | [feature-sdk-adoption](./feature-sdk-adoption.md) | Reading a file that may hold a token, password or key. Extract the value into a variable and use it; never let the raw file reach stdout or a tool result. |
