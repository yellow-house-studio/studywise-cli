# PR Review Checklist

## Purpose
This checklist ensures all PRs meet the project's code hygiene, architecture, and testing standards. Reviewers (including AI reviewers) should verify each item.

---

## 1. Automated Verification (CI must pass)

- [ ] All tests pass (`dotnet test`)
- [ ] Build succeeds (`dotnet build`)
- [ ] No compiler warnings
- [ ] Lint/analyzer issues resolved (if configured)

---

## 2. Command Pattern Compliance

All CLI commands must follow the established pattern:

- [ ] Class has `[AutoRegisterCommand]` attribute
- [ ] Uses static `Create()` method (no instance)
- [ ] Handler uses `context.BindingContext.GetRequiredService<T>()` for DI
- [ ] No static service locators (`CommandServices`, global singletons)

**Correct:**
```csharp
[AutoRegisterCommand]
public sealed class MyCommand
{
    public static Command Create()
    {
        var command = new Command("mycmd", "Description");
        command.SetHandler(async context =>
        {
            var httpClientFactory = context.BindingContext
                .GetRequiredService<IHttpClientFactory>();
            // ...
        });
        return command;
    }
}
```

---

## 3. Configuration Standards

- [ ] No hardcoded magic strings for API URLs, use `StudywiseDefaults`
- [ ] Configuration via `ApplicationConfig` when possible
- [ ] Constants centralized in `Configuration` or `Defaults` classes

---

## 4. JSON Serialization

- [ ] Use `JsonReporter.Format<T>()` from `Studywise.Cli.Formatting`
- [ ] Do not create project-specific JSON formatters
- [ ] `JsonOptions.Default` provides shared serialization settings

---

## 5. Test Coverage

For any feature, ensure tests exist at appropriate layers:

| Layer | What's Tested | Process |
|-------|---------------|---------|
| **Unit** | Isolated logic, formatters, check implementations | Same process |
| **Integration** | Handler logic with mocked HTTP (WireMock.Net) | Same process |
| **E2E** | CLI behavior as separate process | Separate CLI spawn |

- [ ] Unit tests for logic/formatters
- [ ] Integration tests for command handlers with HTTP mocking
- [ ] E2E test for CLI smoke test (spawn process, check exit code/output)

**E2E test rules:**
- Exactly **one** E2E test per feature (as per strategy)
- Uses Dev Proxy for HTTP mocking (not integration test approach)

---

## 6. Acceptance Criteria Verification

For each issue/PR, verify:

- [ ] All acceptance criteria from the feature plan are implemented
- [ ] Manual verification commands documented and run
- [ ] **Each acceptance criterion has an automated test covering it**
- [ ] Tests are named/designed to map to specific acceptance criteria

**Example mapping:**
| Acceptance Criterion | Test |
|---------------------|------|
| "Exit code 0 when all checks pass" | `DoctorCommandTests.InvokeAsync_WithJsonFlag_ReturnsSuccessWhenNoChecksFail` |
| "Exit code 1 when any check fails" | `DoctorCommandTests.InvokeAsync_ReturnsFailureWhenApiKeyMissing` |
| "`--json` flag outputs machine-readable JSON" | `E2ETests.DoctorJson_OutputsMachineReadableDiagnostics` |

For new features, the PR description should include a table mapping each criterion to its test(s). If a criterion cannot be tested automatically, note why and document manual verification instead.

---

## 7. Documentation Updates

- [ ] Architecture docs updated if new patterns introduced
- [ ] No orphaned documentation (update or remove old docs)

---

## 8. Commit Hygiene

- [ ] Atomic commits (one logical change per commit)
- [ ] Commit messages follow convention: `type: description`
- [ ] No "WIP" or debug commits in PR

---

_Created 2026-05-10 as part of PR #11 review standards_