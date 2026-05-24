# After-Action Report: `--check` Flag for Selective Doctor Checks

**Issue:** [#8](https://github.com/yellow-house-studio/studywise-cli/issues/8)  
**PR:** [#19](https://github.com/yellow-house-studio/studywise-cli/pull/19)  
**Date:** 2026-05-22  
**Feature:** `--check` flag for `studywise doctor` command  

## Traceability
- Issue: https://github.com/yellow-house-studio/studywise-cli/issues/8
- PR: https://github.com/yellow-house-studio/studywise-cli/pull/19
- Plan: `docs/plans/08-check-flag.md`

---

## What We Built

Added `--check` option to `studywise doctor` command allowing users to run a specific diagnostic check:
- `--check config` — runs only the config check
- `--check api-key` — runs only the API key check
- `--check connection` — runs only the connection check
- `--check all` (default) — runs all checks in order

Case-insensitive check name matching. Unknown check names produce `unknown check: <name>` on stderr with exit code 1.

---

## What Didn't Go Well

### 1. Critical Binding Bug Caught by AI Review

**Problem:** The initial implementation failed to pass `checkOption` to `SetHandler` AND didn't call `GetValueForOption(checkOption)`. The `--check` option was silently ignored.

**Example of the bug:**
```csharp
// WRONG — checkOption missing from SetHandler
SetHandler(async (context, cancellationToken) =>
{
    var jsonOption = context.ParseResult.GetValueForOption(jsonOption);
    // checkName never extracted — will always be default "all"
    var options = new DoctorCommandOptions(jsonOption, "all");
}, jsonOption);  // checkOption not passed!
```

**Fix required:**
```csharp
// CORRECT
SetHandler(async (context, cancellationToken) =>
{
    var jsonOption = context.ParseResult.GetValueForOption(jsonOption);
    var checkName = context.ParseResult.GetValueForOption(checkOption);  // ← MUST call this
    var options = new DoctorCommandOptions(jsonOption, checkName);
}, jsonOption, checkOption);  // ← checkOption MUST be passed
```

**Prevention:** This pattern (Option binding to SetHandler) is a common System.CommandLine mistake. Should be documented in CLI project skills.

### 2. Missing Project Skills Infrastructure

**Problem:** studywise-cli had no `.opencode/skills/` directory and no post-merge workflow skills. The project skills infrastructure was not set up.

**Impact:** Post-merge documentation could not follow the standard skill-driven workflow.

**Prevention:** When a new repo is added to repos.md, ensure `.opencode/skills/` directory structure exists with essential skills.

---

## What Went Well

1. **Clear plan:** The plan document (`08-check-flag.md`) was comprehensive and included the critical binding detail as a ⚠️ callout — but the implementation missed it.
2. **Good test coverage:** Unit tests for `DoctorCommandHandler` filtered correctly.
3. **Fast AI Review turnaround:** GPT-5.5 caught the binding bug quickly.
4. **Case-insensitive matching:** Implemented cleanly with `ToLowerInvariant()`.

---

## Improvements for Next Time

### For Implement-Feature Skill (studywise-cli)

Add to the project skills a **System.CommandLine binding checklist** that must be verified:

```
## CLI Option Binding Checklist
When adding a new Option<T> to a command:
□ Option is added via AddOption()
□ Option is passed to SetHandler(bindingDelegate, option1, option2, ...)
□ GetValueForOption(option) is called in the handler
□ Handler uses the extracted value, not a hardcoded default
```

### For Plan Review

When reviewing a plan that involves System.CommandLine options, explicitly verify:
1. The plan shows how the option binds to the handler (passing to SetHandler)
2. The plan shows `GetValueForOption()` being called
3. The handler uses the extracted option value, not a literal default

### For New Repo Onboarding

Before a repo can participate in post-merge workflow:
1. Ensure `.opencode/skills/` directory exists
2. Copy essential skills from another project's `.opencode/skills/`
3. Create initial project skills structure

---

## Process Mistakes We Want to Avoid

| Mistake | Prevention |
|---------|------------|
| Option not passed to SetHandler | Add to CLI binding checklist in skills |
| Option added to SetHandler but GetValueForOption not called | Add to CLI binding checklist in skills |
| New repo lacks post-merge infrastructure | Add onboarding step when adding to repos.md |

---

## Related Artifacts

- Feature Plan: `docs/plans/08-check-flag.md`
- Implementation: `src/Studywise.Cli/Commands/Doctor/`
- Tests: Unit tests for `DoctorCommandHandler` and `DoctorCommand`