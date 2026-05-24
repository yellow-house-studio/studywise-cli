---
name: plan-cli-feature
description: "Planning skill for studywise-cli features. Ensures CLI-specific patterns are captured in plans."
---

# Plan CLI Feature

## Context
When creating a plan for a CLI feature in studywise-cli, use this skill to ensure CLI patterns are properly documented.

## CLI-Specific Requirements

### System.CommandLine Options

For every new CLI option, the plan MUST document:

1. **Option declaration** — name, type, default value, description
2. **SetHandler binding** — show the option being passed to SetHandler
3. **GetValueForOption call** — show the option being extracted in the handler

**Template for option documentation:**
```markdown
### `{option}` Option

Declaration:
```csharp
var {camelName}Option = new Option<{type}>(
    name: "--{kebab-name}",
    description: "{description}",
    getDefaultValue: () => {default});
AddOption({camelName}Option);
```

Binding:
```csharp
SetHandler(async (context, cancellationToken) =>
{
    var {camelName} = context.ParseResult.GetValueForOption({camelName}Option);
    // ... use {camelName}
}, {camelName}Option);  // ← pass to SetHandler
```

Common mistakes:
- ❌ Option not passed to SetHandler — option silently ignored
- ❌ GetValueForOption not called — default value used instead of user input
- ❌ Handler uses hardcoded value instead of extracted option
```

### Exit Codes

CLI commands must define exit codes:
- `0` — success
- `1` — failure (with descriptive error message)

### Output Format

Consider:
- stdout for data output
- stderr for error messages
- `--json` flag for machine-readable output when appropriate

## Plan Template

```markdown
# Issue #{number}: {Title}

## Purpose
One-paragraph description of what this feature does and why.

## Scope
- In scope: {bullet list}
- Out of scope: {bullet list}

## Functional Requirements
### {Feature name}
{GIVEN/WHEN/THEN format for each requirement}

## Code Design
### Option Declaration
{csharp code}

### Handler Binding  
{csharp code}

### Check Resolution / Business Logic
{csharp code if applicable}

## Exit Codes
| Scenario | Code |
|----------|------|
| Success | 0 |
| Failure | 1 |

## Verification
```bash
dotnet test --filter "FullyQualifiedName~{TestArea}"
dotnet run --project src/Studywise.Cli -- {command} --{option} {value}
```

## Acceptance Criteria
{Checklist format}
```

## Review Checklist

Before submitting plan for AI review:
- [ ] Every option has SetHandler binding shown
- [ ] Every option has GetValueForOption call shown  
- [ ] Exit codes defined
- [ ] Verification commands provided
- [ ] GIVEN/WHEN/THEN acceptance criteria included