# Retrospectives

Write-ups capturing corrections, lessons, and cross-cutting implications
from closed work. Both modes are opened as a follow-up PR **after** the
work PR lands (see AGENTS.md §"Self-Improvement Loop").

**This file is the CLI's own convention.** The shared, org-wide procedure
lives in the `create-retrospective` skill (`swe-skills-and-docs`) — what to
capture and how to review a retrospective. The paths, the resolution rule
below and the topical index are this repo's, because the CLI does its own
retrospectives for its own code.

| Mode | File pattern | Trigger | Scope |
|---|---|---|---|
| **Issue retrospective** | `issue-<N>-<slug>.md` | Any merged PR that closed a GitHub issue AND had user corrections during the work | One issue. Corrections only. |
| **Feature retrospective** | `feature-<slug>.md` | A feature warrants a fuller postmortem | One feature. Broader scope. |

## Naming

- Issue retro: `docs/retrospective/issue-<N>-<slug>.md` — N is the GitHub
  issue number.
- Feature retro: `docs/retrospective/feature-<slug>.md`.

Findability by issue number is the entire point: `issue-<N>-` sorts, greps
and auto-links.

## When

- A follow-up PR, **after** the work PR is merged. Never bundled into the
  work PR.
- One retrospective per merged PR or feature.

## The correction structure

Each correction is four fields:

- **Pattern** — when does this shape recur? (Keywords, so a session-start
  review can find it.)
- **The mistake** — mechanical, no editorializing. What was done wrong.
- **The rule** — the corrected behavior.
- **Applies when** — trigger shape for the topical index.

Skip trivial corrections (a rename, a typo). Capture everything else —
topical filtering is the reader's job, not yours.

## No editorializing

Capture what was corrected, not whether the work felt good or bad.

## "Actions taken" — and when an action is not an issue

Every entry in the retrospective's `Actions taken` section is resolved
before merge. Three resolutions:

- `[x]` — done in this PR (link the commit or PR), OR
- `[x]` — **filed as a GitHub issue** that the retrospective links to, OR
- `[x]` — **recorded as a runbook entry** (an operator action outside this
  repo; link the doc).

An unchecked `- [ ]` at merge time is a regression — the retrospective is
half-useful and the lesson is unlikely to be picked up. Self-check before
opening the PR, against the file you just wrote:

```bash
grep -E '^- \[ \]' docs/retrospective/<the-new-retro>.md
# Expected: no output.
```

### Scope gate — one question before you file anything

> **Does this repo's code change?**

| Answer | Resolution |
|---|---|
| **Yes** — a commit, a config file, a workflow, a script, a doc the build reads | Tracked issue, with the exact reproduction steps. |
| **No** — it is an operator action outside the repo: rotate a token, land an Azure setting, edit a secret store | **No issue by default.** The recipe goes in the retrospective and in the repo's own runbook, where the operator actually reads it. |
| **No**, but it has a deadline, recurs on a schedule, spans repos or owners, or must be handed to someone else to execute | Tracked issue — filed in the repo that owns the affected surface, labelled as an operational chore rather than a feature, and cross-linked from the other repos' retros. |

One-line test: *an issue body with no code change, no failing test and no
surface this repo owns is not a code-repo issue — it is a runbook entry.*

**Why the gate exists.** A tracker is a work queue for code. An operator
chore filed next to code work reads as "someone will pick this up with the
code", ages in a backlog nobody works, and — when the work touches
credentials or surfaces in several repos — is invisible to the repos that
own the affected copies. The retrospective is the durable record; a runbook
is the durable procedure.

**Worked example.** The GitHub Packages PAT rotation (2026-10-22) was filed
as an issue in this repo, while the same rotation had to be applied to
credentials in three other repos and to every machine with Proton Pass
access — neither of which the tracker showed anyone. It is a runbook entry
now: [`docs/devenv/github-packages-pat-rotation.md`](../devenv/github-packages-pat-rotation.md).

## Update the topical index at PR-creation time, not at merge time

Session-start review reads the index below to find the relevant lesson. A
stale index means the lesson never gets applied.

---

## Templates

### Issue retrospective

```markdown
---
tags: [tag-1, tag-2, tag-3]
---

# Retrospective: #<N> — <title>

> **Issue:** #<N>
> **Closing PR:** #<M>
> **Date:** YYYY-MM-DD

## Outcome
<one line: merged / closed wontfix / superseded by ...>

## What we set out to do
- ...

## What actually happened
- ...

## Corrections

### <short slug for the lesson>
- **Pattern:** ...
- **The mistake:** ...
- **The rule:** ...
- **Applies when:** ...

## Cross-cutting implications
- <other repos / docs / issues affected>

## Actions taken
- [x] <action> — <done in this PR / issue #N / runbook doc>

## Related
- #<N>, #<M>
```

### Feature retrospective

```markdown
---
tags: [feature-<slug>, <area>, <skill-failure-tag>]
---

# Retrospective: <Feature Name> (<issue #> / PR #<PR#>)

> **Feature:** <name>
> **Closing PR:** #<PR#>
> **Issue:** #<issue#>
> **Date:** YYYY-MM-DD

## Outcome
...

## What we set out to do
- ...

## What actually happened
- ...

## What Went Well
- <positive observation — keep mechanical, no praise>

## What Didn't Go Well
- <issue 1 — must name the skill that failed and the behavior change needed>

### Actionability rule
For each "What Didn't Go Well" entry:
1. **Name the skill that failed** (e.g. `implement-feature` did not verify scope).
2. **Specify the behavioral change** (e.g. add a scope-verification gate to `implement-feature`).
3. **Link to an improvement doc or a follow-up issue.**

Generic complaints are useless — specific, actionable improvements are the
only value of a feature retrospective.

## Corrections
<Same Pattern / The mistake / The rule / Applies when structure, one block per correction.>

## Cross-cutting implications
- <other repos / docs / issues affected>

## Validation Checklist

### Plan Compliance
- <planned item> — Completed / Not completed / Modified

### Files Created/Modified
| File | Type | Purpose |
|------|------|---------|
| path/to/file | Created/Modified | Description |

### Test Coverage
The CLI has two layers — Unit and Integration. There is no E2E layer here.
- **Unit:** Passed / Failed / Not run
- **Integration:** Passed / Failed / Not run

### Build Status
- Build passes with 0 warnings/errors

## Actions taken
- [x] <action> — <done in this PR / issue #N / runbook doc>

## Related
- #<issue#>, #<PR#>
```

---

## Why this folder exists

It did not, until 2026-10-22.

During the SDK adoption work a credential was exposed in tool output — the
same failure mode already captured in another repo's retrospective for the
same class of secret. That lesson was not reachable from this repo: there
was no retrospective index here to point at it. The folder exists so the
next reader in this repo can find the lesson without knowing which other
repo wrote it.

## Topical index

| Topic | Retrospective | Applies when |
|---|---|---|
| A 404 from a package feed is not evidence of absence | [feature-sdk-adoption](./feature-sdk-adoption.md) | Checking whether a package version exists on a private or authenticated feed (GitHub Packages, Azure Artifacts, a private NuGet source). Verify with an authenticated request to the version-index endpoint, and run the actual restore before asserting that something was never published. |
| A tool's exit code is not the answer — do the real operation | [feature-sdk-adoption](./feature-sdk-adoption.md) | Reporting that a merge, a build or a merge-order check is clean based on a dry-run or a tool's exit status. Perform the real merge in a real clone and read the result. |
| Another team's obligation to us is the surface we consume | [feature-sdk-adoption](./feature-sdk-adoption.md) | Filing an issue in another repo because their internal pipeline or tooling is broken. Their obligation to us is the API or SDK surface we consume; their internal machinery is theirs. |
| Lead with verified state, not a prediction | [feature-sdk-adoption](./feature-sdk-adoption.md) | Writing a bug report whose headline is what a tool *will* compute or produce. State the verified condition; the prediction belongs in a note, if at all. |
| Never print a credential-bearing config into tool output | [feature-sdk-adoption](./feature-sdk-adoption.md) | Reading a file that may hold a token, password or key. Extract the value into a variable and use it; never let the raw file reach stdout or a tool result. |
| An operator action outside the repo is a runbook entry, not a tracker issue | [§ Scope gate](#scope-gate--one-question-before-you-file-anything) | Before filing a tracked issue for work outside this repo — rotate a token, land an Azure setting. Ask: does this repo's code change? If not, the recipe goes in the retrospective and the repo's runbook. File an issue only when the action has a deadline, recurs, spans repos or owners, or must be handed off — and then in the repo that owns the affected surface. |
