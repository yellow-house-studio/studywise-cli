# Studywise CLI Test Strategy

## Status
**Datum:** 2026-09-29 (rewritten after the NUnit migration)
**Typ:** Testdokumentation
**Projekt:** Studywise CLI

---

## 1. Source of truth

This document describes how the CLI repo specifically implements the
org-wide test strategy at `docs/test_strategy.md` (in the org
CodeStandards repo at `/mnt/c/Users/minma/Projects/CodeStandards`).
The org doc is the source of truth for *what* a YHS project must do;
this doc only adds the CLI-specific layers, package versions, and
enforcement details.

---

## 2. Test layers

The CLI repo has **two** test projects. There is no E2E layer for a
CLI binary — per the org strategy, E2E belongs in the frontend repo
(Playwright).

```
test/
├── Studywise.CLI.UnitTests/           # In-process, no HTTP
└── Studywise.CLI.IntegrationTests/    # In-process with embedded WireMock
```

| Lager | Process | HTTP | Framework | What it tests |
|-------|---------|------|-----------|----------------|
| UnitTests | Test process | ❌ | NUnit 5 + FluentAssertions 7 | Domain logic + handlers in isolation, no external dependencies |
| IntegrationTests | Test process | ✅ WireMock.Net 2.18 embedded | NUnit 5 + FluentAssertions 7 | Doctor command handler end-to-end against an in-process HTTP mock |

---

## 3. Packages

Latest stable on the public NuGet gallery (2026-09-29). All four
of these are pinned in the test csprojs; bumps require a coordinated
migration PR.

| Package | Version | Why this version |
|---------|---------|------------------|
| `NUnit` | **5.0.0** | Latest stable. MIT-licensed. Supports `[Category]` filtering. |
| `NUnit3TestAdapter` | **6.3.0** | Latest stable. |
| `NUnit.Analyzers` | **4.15.0** | Latest stable. |
| `FluentAssertions` | **7.2.0** | Last fully-open-source major (8.x is paid for commercial use). |
| `FluentAssertions.Analyzers` | **0.34.1** | Latest stable. |
| `Moq` | **4.21.0** | Latest stable. Same family as Studywise-Api / SparkProgress. |
| `WireMock.Net` | **2.18.0** | Latest stable. Integration tests only. |
| `coverlet.collector` | **10.1.0** | Latest stable. |
| `Microsoft.NET.Test.Sdk` | **18.10.1** | Latest stable. |

NUnit.Framework and FluentAssertions are added as `<Using Include>`
items in each test csproj so test files don't need per-file imports
for `[Test]` / `[Category]` / `Should()`.

---

## 4. Categories

Every test class carries a `[Category(...)]` attribute:

```csharp
[Category("Unit")]            // Studywise.CLI.UnitTests
[Category("Integration")]     // Studywise.CLI.IntegrationTests
```

`verify.sh` and the CI workflows currently run all categories. If a
test ever needs to be opt-out (e.g. a slow benchmark), add
`[Category("LongRunning")]` and tighten `--scope=fast` to
`--filter "Category!=LongRunning"`. No such tests exist today.

Filter commands (for ad-hoc local runs):

```bash
dotnet test --filter "Category=Unit"
dotnet test --filter "Category=Integration"
dotnet test --filter "Category=Unit|Category=Integration"
```

---

## 5. Assertion style

FluentAssertions. Every assertion reads as `subject.Should().X(...)`.

```csharp
result.Status.Should().Be(DiagnosticStatus.Pass);
result.Message.Should().Contain("OK");
output.Should().Contain("All checks passed");
checks[0].GetProperty("name").GetString().Should().Be("config");
new[] { "pass", "warn", "fail" }.Should().Contain(status);

await act.Should().ThrowAsync<OperationCanceledException>();
collection.Should().HaveCount(3);
collection.Should().Equal("config", "api-key", "connection");
result.Message.Should().NotContain(secretValue);
```

For exception assertions in async tests, prefer
`act.Should().ThrowAsync<T>()` over `Assert.ThrowsAsync<T>` —
the latter doesn't get the FluentAssertions failure rendering.

---

## 6. Integration tests — never touch the real API

**Hard rule:** integration tests must never call the real Studywise API.
This is enforced by test pattern, not a runtime guard.

Why a runtime guard isn't used: the existing integration tests build
their own `WireMockServer.Start()` instance and capture its loopback
URL inside the test body, then set `STUDYWISE_API_BASE_URL` to that
URL. A `[OneTimeSetUp]` guard reading the env var would always see
the unset value (the test sets it *after* `[OneTimeSetUp]` runs) and
fail every test.

The actual safety mechanism is the test pattern itself:

1. Each integration test does `WireMockServer.Start()` to bind a
   loopback port.
2. The test sets `STUDYWISE_API_BASE_URL` to that URL.
3. The test builds a fresh `IHttpClientFactory` pointed at the URL
   via `new ServiceCollection().AddHttpClient(...).BaseAddress = ...`.
4. `ConnectionDiagnosticCheck(httpClientFactory)` reads from the
   factory, so it never sees `https://api.studywise.io`.

The test base class `BaseIntegrationTest` exists as a marker. If a
future integration test depends on a URL provided by surrounding CI
config rather than constructing its own factory, add a loopback-URL
assertion in `[OneTimeSetUp]` of that fixture — see the xmldoc on
`BaseIntegrationTest` for the pattern.

---

## 7. Test projects

### `Studywise.CLI.UnitTests`

In-process tests for the domain logic + application handlers.
Project structure mirrors the production assembly's namespace tree
(`Studywise.Cli.Diagnostics`, `Studywise.Cli.Commands`, etc.).

### `Studywise.CLI.IntegrationTests`

In-process tests that exercise the full `Doctor` command against a
mocked HTTP backend. Uses `WireMockServer.Start()` for in-process
HTTP mocking.

---

## 8. CI / local

- **Local**: `./scripts/verify.sh --scope=integration` runs every
  gate the pre-push hook runs. `--scope=full` adds the coverage gate.
- **PR**: `.github/workflows/ci-fast.yml` runs `verify.sh
  --scope=integration` on PR open / synchronize / reopen.
  `.github/workflows/ci-full.yml` runs `verify.sh --scope=full` on
  approval.
- **Post-merge**: `.github/workflows/update-baseline-on-merge.yml`
  runs `verify.sh --scope=integration --skip-coverage-gate` and
  commits the auto-raised coverage baseline if it improved.
- **Coverage gate** lives in `verify.sh` Phase 7. Per-assembly drop
  tolerance is 1pp; new files must have ≥80% line coverage.

No `STUDYWISE_API_KEY` secret is needed for any local or CI run.
The CLI's auth is exercised end-to-end through the `DoctorCommand`
handler, which reads the key from `ApplicationConfig` (env var +
`~/.config/studywise/config.json` fallback) — but the integration
tests pass the key directly into the handler and don't dial the real
API to validate it.

---

## 9. How to run

```bash
# Install toolchain (one-time per machine)
./scripts/setup-env.sh

# Fast inner loop
./scripts/verify.sh --scope=unit

# Pre-push gate (mirrors what the pre-push hook runs)
./scripts/verify.sh --scope=integration

# Full CI mirror (also runs coverage gate)
./scripts/verify.sh --scope=full

# Category-filtered runs (when you only want one layer)
dotnet test --filter "Category=Unit"
dotnet test --filter "Category=Integration"
```

---

## 10. Verksamhetsordlista (organisationsstandard)

| Svenskt ord | English | Betydelse |
|-------------|---------|-----------|
| Täckningskrav | Coverage requirement | Minimum coverage % required per layer. Domain 100%, Application 80%+. |
| Domänlogik | Domain logic | Pure business rules; no I/O. Unit-tested at 100%. |
| Handlers | Application handlers | Orchestrate domain + I/O. Unit + Integration-tested. |
| Snabb feedback | Fast feedback | Test runs < 5 min, run on every commit/PR. |
| CI/CD-steg | CI/CD stage | A pipeline step (not a test type). Smoke = CI step, not a layer. |

---

_Skriven 2026-05-09. Omarbetad 2026-09-29 efter migrering till NUnit 5 + FluentAssertions 7._
