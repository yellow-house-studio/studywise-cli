---
name: create-after-action-report
description: "Creates after-action report for studywise-cli features. Analyzes implementation and documents learnings."
---

# Create After-Action Report

## Context
After a feature PR is merged, create an after-action report documenting what went well, what didn't, and process improvements.

## Input
- Issue number
- PR number
- Feature name
- Implementation files changed

## Output
`docs/after-action-report/{feature-name}.md`

## Workflow

### Step 1: Gather Information
```bash
git log origin/main..HEAD --oneline  # implementation commits
gh pr view {pr} --json body,comments  # PR description and review comments
git diff origin/main..HEAD --name-only  # files changed
```

### Step 2: Identify Process Issues
Read the feature plan and look for:
- Places where the plan was unclear or incomplete
- Implementation mistakes caught by review
- Missing test coverage
- Binding/parameter issues (common in CLI)

### Step 3: Document Learnings

Create `docs/after-action-report/{feature-name}.md` with:

```markdown
# After-Action Report: {Feature Name}

## What We Built
Brief description of the feature.

## What Didn't Go Well
### {Issue 1}
**Problem:** {what went wrong}
**Why:** {root cause}
**Prevention:** {how to prevent}

## What Went Well
- {bullet points}

## Improvements for Next Time
{bullet points of process improvements}

## Process Mistakes We Want to Avoid
| Mistake | Prevention |
|---------|------------|
| {mistake} | {prevention} |
```

### Step 4: Create Feature Docs
Also create `docs/features/{feature-name}/README.md` with feature documentation.

### Step 5: Link to Plan
Add traceability header to AAR:
```markdown
## Traceability
- Issue: https://github.com/yellow-house-studio/studywise-cli/issues/{number}
- PR: https://github.com/yellow-house-studio/studywise-cli/pull/{pr}
- Plan: `docs/plans/{number}-{short-name}.md`
```
