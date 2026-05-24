---
name: finalize-feature-docs
description: Orchestration skill to create all post-PR-merge documentation. Runs create-feature-docs, create-after-action-report, and update-api-wiki in order.
---

## Overview

This skill orchestrates the creation of all documentation after a feature PR is merged. Run this after merge to create:
1. Internal feature docs (create-feature-docs)
2. After-action report with learnings (create-after-action-report)
3. Consumer API documentation (update-api-wiki)

## When to Use

Use this when:
- A feature PR has been merged to master
- You need to create all post-merge documentation
- You want a complete documentation package for the feature

## Prerequisites

- [ ] Feature PR is merged to master
- [ ] You have the feature name and PR/issue number
- [ ] Feature plan exists at `docs/feature-planning/feature-<name>.md` (canonical location)
- [ ] Technical flow exists at `docs/technical-flows/<name>.md` (or explicitly document that it is missing)

## Resume Workflow (If Branch Already Exists)

If resuming a finalize-docs branch, start with:
1. Read `docs/after-action-report/<feature-name>.md` first.
2. Extract process mistakes and convert them into concrete updates to skills/docs.
3. Verify feature docs and API wiki status before creating any new commits.

## Workflow

### Step 0: Branch Verification (BEFORE ANYTHING ELSE)

**⚠️ Verify you are on the correct branch before starting.**

1. Check current branch: `git branch --show-current`
2. If you are NOT on `master`, checkout master and pull first:
   ```bash
   git checkout master && git pull
   ```
3. Confirm you are starting from a clean master state
4. If resuming from a previous incomplete run, check `git status` for any uncommitted work and decide whether to continue on the existing branch or start fresh

**Starting a fresh finalize branch:**
```bash
git checkout -b docs/<feature-name>-finalize
```

### Step 1: Gather Information

Before running individual skills, gather:

1. **Feature name and branch/PR number**
2. **Files created/modified:** `git diff master..HEAD --name-only`
3. **Related GitHub issues:** `gh issue list --state closed --search "<feature>"`
4. **Feature plan:** Read `docs/feature-planning/feature-<name>.md`
5. **Technical flow:** Read `docs/technical-flows/<name>.md` (if present)
6. **Issue traceability check:** Ensure issue includes concrete links to plan/artifacts and current file paths
7. **After-action report:** Read `docs/after-action-report/<feature-name>.md` if it exists

**Process improvement check:** Read the after-action report's "What Didn't Go Well" and "Process Mistakes We Want to Avoid" sections. For each mistake listed, check if a corresponding entry already exists in `docs/improvement-analysis/`. If not, flag it for creation as part of this finalize run.

### Step 2: Create Feature Docs

Run **create-feature-docs** skill:
- Creates `docs/features/<feature-name>/README.md`
- Creates `docs/features/<feature-name>/cli-commands.md` (if CLI)
- Creates `docs/features/<feature-name>/api-endpoints.md` (if API)

### Step 3: Create After-Action Report

Run **create-after-action-report** skill:
- Creates `docs/after-action-report/<feature-name>.md`
- Analyzes git history, plans, logs, reviews, fixes

### Step 3.5: Apply Finalize Traceability Header (REQUIRED)

Every finalize artifact must start with a traceability header near the top of the file.

Use this template in each artifact (`docs/features/...`, `docs/after-action-report/...`, and any API wiki page updates):

```markdown
## Traceability
- Issue: https://github.com/yellow-house-studio/studywise-api/issues/<issue-number>
- PR: https://github.com/yellow-house-studio/studywise-api/pull/<pr-number>
- Plan: `docs/feature-planning/feature-<name>.md`
```

Rules:
- Place this block before deep implementation details (ideally right after the overview section).
- Use canonical plan path under `docs/feature-planning/` only.

### Step 4: Update API Wiki

Run **update-api-wiki** skill only if the feature changed consumer-facing API endpoints:
- Updates `docs/studywise-api.wiki/API-Reference/<Resource>.md`
- Adds new endpoints to consumer-facing docs

If the feature is CLI-only or internal-only, explicitly record: "API wiki update not required (no API contract changes)."

### Step 4.5: Documentation Completeness Check

Before commit/PR, verify:
- Feature docs exist under `docs/features/<feature-name>/`
- After-action report exists and includes actionable process improvements
- Missing optional artifacts (for example, technical flow) are called out explicitly
- Issue/pr references in docs use correct paths and links

**Traceability Gate (REQUIRED):**
- Verify issue comments include current plan path and PR link
- Verify plan references point to canonical `docs/feature-planning/feature-<name>.md`
- Verify finalize docs include a "Related" section with:
  - Link to feature plan (or explicit "No plan" note)
  - Link to technical flow (or explicit "No technical flow" note)
  - Link to issue
  - Link to PR
- Verify finalize docs include the required `## Traceability` header with `Issue`, `PR`, and `Plan` links near the top
- If any traceability is missing, add it before finalizing

### Step 5: Commit and Create PR

```bash
git checkout -b docs/<feature-name>-finalize
git add docs/features/ docs/after-action-report/ docs/studywise-api.wiki/
git commit -m "docs: add final documentation for <feature-name>

- Feature docs: docs/features/<feature-name>/
- After-action report: docs/after-action-report/<feature-name>.md
- API wiki updates: docs/studywise-api.wiki/API-Reference/"

git push -u origin docs/<feature-name>-finalize
```

### Step 6: Create PR

```bash
gh pr create --repo yellow-house-studio/studywise-api \
  --title "docs: finalize documentation for <feature-name>" \
  --body "## Summary
Documentation for completed feature <name>:

- Internal feature docs: \`docs/features/<feature-name>/*\`
- After-action report: \`docs/after-action-report/<feature-name>.md\`
- API wiki updates: \`docs/studywise-api.wiki/API-Reference/<resource>.md\`

Closes #<ISSUE_NUMBER>"
```

## Document Locations

| Document | Location |
|----------|----------|
| Feature Plan (canonical) | `docs/feature-planning/feature-<name>.md` |
| Feature README | `docs/features/<feature-name>/README.md` |
| CLI Commands | `docs/features/<feature-name>/cli-commands.md` |
| API Endpoints | `docs/features/<feature-name>/api-endpoints.md` |
| After-Action Report | `docs/after-action-report/<feature-name>.md` |
| API Wiki | `docs/studywise-api.wiki/API-Reference/<Resource>.md` |

## Related Skills

- **create-feature-docs**: Creates internal feature documentation
- **create-after-action-report**: Creates learnings documentation
- **update-api-wiki**: Updates consumer-facing API docs
