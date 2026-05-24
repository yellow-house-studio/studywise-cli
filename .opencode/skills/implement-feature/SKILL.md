---
name: implement-feature
description: "CLI implementation workflow for studywise-cli. Uses TDD: write failing tests first, then implement to make them pass."
---

# Implement Feature (studywise-cli)

## Context
This skill is for implementing features in the studywise-cli project after the plan has been approved (issue moved to Ready).

## Workflow

### Step 1: Create Feature Branch

```bash
cd ~/projects/studywise-cli
git checkout main && git pull
git checkout -b feature/issue-{number}-{short-name}
```

### Step 2: Read the Plan

Read `docs/plans/{number}-{short-name}.md` before writing any code.

### Step 3: Write Failing Tests First (TDD)

Run the test for the area you'll modify to see a failing test:
```bash
dotnet test --filter "FullyQualifiedName~{Area}" --no-build  # verify test exists
```

Write the test first, verify it fails, then implement.

### Step 4: Implement

Follow the plan. For CLI commands specifically:

**System.CommandLine Critical Pattern — OPTIONS MUST BIND:**
```csharp
// When adding a new Option<T>:
var myOption = new Option<string>(name: "--my-option", getDefaultValue: () => "default");
AddOption(myOption);

// CRITICAL: Pass to SetHandler AND call GetValueForOption
SetHandler(async (context, cancellationToken) =>
{
    var myValue = context.ParseResult.GetValueForOption(myOption);  // ← MUST call this
    var options = new MyOptions(myValue);
    await HandleAsync(options, cancellationToken);
}, myOption);  // ← Option must be passed here
```

**Common mistakes to avoid:**
- ❌ `SetHandler(handler, jsonOption)` without passing `myOption`
- ❌ Not calling `GetValueForOption(myOption)` — option silently uses default
- ❌ Handler uses hardcoded string instead of extracted option value

### Step 5: Build and Test

```bash
dotnet build
dotnet test --filter "FullyQualifiedName~{Area}"
```

### Step 6: Commit and Push

```bash
git add -A && git commit -m "feat(#{issue}): {short description}"
git push -u origin feature/issue-{number}-{short-name}
```

## Exit Criteria
- All tests pass
- Manual verification of the feature works as described in plan
- Commit pushed to origin