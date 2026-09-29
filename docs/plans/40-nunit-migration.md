# Plan: align CLI testing with YHS standard (NUnit + FluentAssertions + WireMock only)

**Issue:** none (no GitHub issue tracked; work grew out of an architecture review)
**Branch:** `chore/nunit-test-migration`
**Scope:** ~25 files, mechanical + a few doc rewrites.

## Why

The CLI repo currently uses xunit + Dev Proxy + a hand-rolled E2E process-spawn layer. That doesn't match how the rest of YHS writes tests (see `docs/test_strategy.md` in the org CodeStandards repo):

- Org standard framework: **NUnit** (4.3.2 in Studywise-Api, 4.1.0 in SparkProgress). Latest stable is 5.0.0.
- Org standard assertions: **FluentAssertions**. Org strategy doc says `YHS.Platform.Testing.FluentAssertions` (internal package); the public sibling Studywise-Api uses **FluentAssertions 7.0.0**. Latest stable is **8.11.0**, but v8 is **paid for commercial use** (free for open-source / non-commercial only). YHS is commercial, so **pin 7.2.0** — the last fully-open-source line that remains supported.
- Org strategy: E2E tests live in the **frontend repo only** (Playwright). A CLI binary does not have E2E in the org sense. The repo's `Studywise.CLI.E2ETests` project is an orphan layer that should not exist.
- Integration tests use **WireMock.Net** in-process. They must never call the real API. The current `DoctorCommandIntegrationTests` is already WireMock-based — we'll keep that pattern and add a guard so it can't regress.

The org strategy doc also documents the `[Category("Fast")]` / `[Category("LongRunning")]` pattern that the api repo uses to split tests for the fast-PR lane vs the merge-time lane. The CLI's `--scope=fast == --scope=full` comment in `verify.sh` is a workaround that should go away once categories exist.

## Package versions (latest stable on the public NuGet gallery, 2026-09-29)

| Package | From | To | Why |
|---|---|---|---|
| `NUnit` | xunit 2.5.3 (or 2.9.2 in E2E) | **5.0.0** | Latest stable. Supports `Category` filtering. |
| `NUnit3TestAdapter` | xunit.runner.visualstudio 2.5.3/2.8.2 | **6.3.0** | Latest stable. |
| `NUnit.Analyzers` | — | **4.15.0** | Latest stable. |
| `FluentAssertions` | — | **7.2.0** | Last fully-open-source release line; commercial-friendly. v8 requires a paid license. |
| `FluentAssertions.Analyzers` | — | **0.34.1** | Latest stable. |
| `Moq` | 4.20.70 | **4.21.0** | Latest stable. |
| `coverlet.collector` | 6.0.0 (Unit/Int) / 6.0.2 (E2E) | **10.1.0** | Latest stable. |
| `Microsoft.NET.Test.Sdk` | 17.8.0 / 17.12.0 | **18.10.1** | Latest stable. |
| `WireMock.Net` | 1.9.0 | **2.18.0** | Latest stable. |
| `Microsoft.Extensions.Http` | 8.0.0 (only in IntTests csproj, unused) | — | Drop — dead ref. |
| `Scriban.Signed` | 7.5.0 (only in IntTests csproj, unused) | — | Drop — dead ref. |
| `System.CommandLine` | 2.0.0-beta4.22272.1 | — | Leave — that's the production dep, unrelated. |

## What goes away

| File / dir | Why |
|---|---|
| `test/Studywise.CLI.E2ETests/` (entire dir) | E2E is frontend-only per org strategy. The single test it contained is a structural smoke test that passes whether or not the CLI works correctly. |
| `test/.devproxy/` (entire dir) | Only used by the E2E project. |
| `docs/deep-dive/cli-e2e-testing-http-mock.md` | Documents the Dev-Proxy E2E pattern that no longer exists. Will delete rather than rewrite. |
| `Microsoft.Extensions.Http` + `Scriban.Signed` from IntTests csproj | Dead references, never used by test code. |

## What stays but is rewritten

| File | Change |
|---|---|
| `test/Studywise.CLI.UnitTests/Studywise.CLI.UnitTests.csproj` | New package set; drop xunit + Moq; add NUnit + FluentAssertions. |
| `test/Studywise.CLI.IntegrationTests/Studywise.CLI.IntegrationTests.csproj` | Same as above + WireMock.Net 2.18.0; drop dead `Microsoft.Extensions.Http` + `Scriban.Signed`. |
| All 8 `*.cs` files under `test/Studywise.CLI.UnitTests/.../` | Mechanical rewrite: `[Fact]` → `[Test]`, `[Theory]` + `[InlineData]` → `[TestCase]`, `Assert.X` → `Should().X`, `using Xunit` → `using NUnit.Framework + FluentAssertions`. Add `[Category("Unit")]` on each class. |
| `test/Studywise.CLI.IntegrationTests/.../DoctorCommandIntegrationTests.cs` | Same mechanical rewrite + add a `BaseIntegrationTest` base class with a `[OneTimeSetUp]` guard: **asserts `STUDYWISE_API_BASE_URL` is a loopback URL** so the test fails fast in CI rather than silently dialing the real API if someone forgets to set the env var. Add `[Category("Integration")]`. |
| `scripts/verify.sh` | `--scope=fast` filter changes from "all tests" to `--filter "Category!=LongRunning"` (matches Studywise-Api convention). Drop `--skip-e2e` flag (no E2E anymore). Drop `STUDYWISE_API_KEY` pre-flight (no E2E to gate). Update the comment block that claims "xunit doesn't have Fast/LongRunning". Drop `RUN_E2E` / `e2e_tests` phase tracking. Keep `--scope=full`, `--scope=integration`, `--scope=unit`. |
| `scripts/verify.sh` test path constants | Drop the `test/Studywise.CLI.E2ETests/...` reference. |
| `.github/workflows/ci-fast.yml` | Comment update: now that `--scope=integration` means unit + integration (all categories), the comment about "E2E skipped" no longer makes sense. Reword. |
| `.github/workflows/ci-full.yml` | Drop the **Dev Proxy setup steps** (no E2E). Drop the **STUDYWISE_API_KEY gating** (`::warning::` block + dual `verify.sh` invocations). Single `verify.sh --scope=full --no-fail-fast` step. The PR-review trigger and docs-only-check stay (they're not E2E-specific). |
| `.github/coverage-baseline.json` | After migration, regenerate on a clean `verify.sh --scope=full` run. Will be its own commit at the end. |

## Docs to rewrite (not just comment)

### `docs/testing/testing-strategy.md` (full rewrite)

Currently: claims xunit, WireMock, Dev Proxy, three layers.

Target: matches `docs/test_strategy.md` in the org standards repo. Specifically:

- Reference the org doc at the top (this is the source of truth).
- Document the CLI-specific layers: **Unit** + **Integration**. No E2E.
- Categories used: `[Category("Unit")]` on every test class. `[Category("Fast")]` is a Studywise-Api convention for "subset of Integration that runs on every PR"; for the CLI, all integration tests are fast (<5s) so the marker isn't needed — but keep the `[Category("Integration")]` marker so future tests can opt into `[Category("LongRunning")]` if any ever get slow.
- Show a sample test in the new style (NUnit + FluentAssertions + WireMock).
- Show the filter command: `dotnet test --filter "Category=Unit"`, `Category=Integration`.
- Note the integration-test guard: must use a loopback URL via `STUDYWISE_API_BASE_URL`.

### `docs/architecture/cli-test-architecture.md` (targeted edit)

- Remove the "xunit doesn't have the Fast/LongRunning attribute pattern" claim — wrong now.
- Remove the `AutoRegisterCommand` reflection flow description (line 95-115). The actual `Program.cs` does manual DI registration; the `AutoRegisterCommandAttribute` exists but is unused dead code. Note that as a separate cleanup task in the "follow-ups" section rather than deleting the attribute here.
- Update the test-layer comparison table: remove the "E2ETests = separate CLI process" row, replace with "IntegrationTests = in-process with WireMock.Net (loopback only)". Add a "Future: CLI E2E belongs in the frontend repo per org strategy" footnote.

### `docs/devenv/setup.md` (targeted edit)

- Drop the "Install Dev Proxy" section.
- Drop the Dev Proxy troubleshooting section.
- Add a brief section explaining the integration-test guard and `STUDYWISE_API_BASE_URL`.

### `AGENTS.md`

- Replace the line `CLI has no Fast/LongRunning split; xunit doesn't have the attribute pattern NUnit uses` with: `CLI's UnitTests and IntegrationTests both run by default. The Integration project also tags every test with `[Category("Integration")]`; future LongRunning tests opt in with `[Category("LongRunning")]`.`
- Update the line that says `--scope=integration` skips E2E — now it just runs Unit + Integration (no E2E to skip).

## Enforcement: integration tests must NEVER touch the real API

Three layers of defense, in order:

1. **Code-level guard (new)**: `BaseIntegrationTest` base class with `[OneTimeSetUp]`. Asserts:
   - `STUDYWISE_API_BASE_URL` is non-empty AND starts with `http://127.0.0.1` or `http://localhost`.
   - On failure, throws with a clear message pointing at `docs/testing/testing-strategy.md`.
2. **No real URL in production code path**: `ConnectionDiagnosticCheck.RunAsync()` uses `httpClientFactory.CreateClient(StudywiseDefaults.ApiName).GetAsync("/health", ...)` — the host comes from the factory's `BaseAddress`, which `Program.cs` sets from `config.ApiBaseUrl` (which itself reads `STUDYWISE_API_BASE_URL`). With no env var set, `Program.cs` defaults to `https://api.studywise.io`. The integration test sets the env var to the WireMock URL before constructing the ServiceCollection — the production code never sees the real URL during the test. If a future change makes the production code ignore `STUDYWISE_API_BASE_URL` and hardcode `https://api.studywise.io`, the guard above would NOT catch it. **Trade-off acknowledged**: the guard is best-effort, not bulletproof. The org-level answer to "don't ever call prod from tests" is reviewer vigilance + the guard.
3. **WireMock assertions + tests never depend on a specific real-data response**: every integration test uses `WireMockServer.Start()` and configures the mocks it needs. There is no "what does the real /health return" path.

## Risks

1. **NUnit 5 vs 4**: NUnit 5 dropped .NET 6 (we're on 10, fine). The API changes that matter for us: `[OneTimeSetUp]` async story (use `Task` returns, fine), `[TestCase]` parameter binding slightly tightened. The migration is mechanical.
2. **FluentAssertions 7 vs 8**: FA 8 changed a lot of overload resolution around `Func<Task>` and async exception assertions. We're on FA 7 — no risk from the v8 changes, but we lose FA 8 features like `BeJsonSerializable`. None of the FA 8 features are required by the existing tests.
3. **WireMock.Net 2.x vs 1.9**: major version bump. API surface used by the integration test (`WireMockServer.Start()`, `Given(Request.Create().WithPath(...))`, `RespondWith(Response.Create().WithStatusCode(...))`) is unchanged in 2.x. Low risk.
4. **Coverage baseline regeneration**: post-migration the merged-coverage line count will shift (NetFx refactor + attribute rewrites). Phase 8's auto-raise will lift the baseline; we'll commit the raised baseline as the last commit on the PR.
5. **FluentAssertions 7 commercial use**: I picked FA 7 over FA 8 because FA 8 is paid. If YHS has a FA 8 license, ping me and I'll bump.
6. **Env-var race in unit tests**: current `ApplicationConfigTests` / `ConfigDiagnosticCheckTests` / `ApiKeyDiagnosticCheckTests` set+restore process-global env vars without xunit `[Collection]` isolation. xunit's per-class default parallelism means tests can stomp each other. NUnit's default is per-fixture (class) parallelism too, so the same risk exists. Mitigation: add a `[NonParallelizable]` attribute or, better, switch to a `[OneTimeSetUp]/[OneTimeTearDown]` pattern that saves+restores env state once per fixture. We're only touching the migration + a small fixture-base refactor — see "Out of scope" below.

## Out of scope (deliberately deferred)

1. Removing the unused `AutoRegisterCommandAttribute.cs`. Separate cleanup PR — doc has a "follow-ups" note now.
2. Adding real CI matrix against multiple .NET versions. `net10.0` only for now.
3. Replacing Moq with NSubstitute. Moq is what the rest of YHS uses (Studywise-Api `4.20.72`, SparkProgress `4.20.70`). Keep Moq, bump version.
4. Per-process env-var isolation in the existing env-var-touching unit tests (smell #4 in my earlier analysis). Would need a `[NonParallelizable]` attribute on each fixture or a `[OneTimeSetUp]` refactor. Adds noise to this PR; file as a follow-up.
5. Coverage improvement work. Current coverage is 165/277 ≈ 60%. The PR does not aim to raise coverage; it migrates the framework. Coverage may shift slightly as line counts change but won't improve structurally.

## Verification plan

For each commit in this PR:

1. `./scripts/verify.sh --scope=integration --no-fail-fast` — must pass.
2. Confirm `git diff --stat` only shows intended files.

After all commits:

3. `./scripts/verify.sh --scope=full --no-fail-fast` — runs the coverage gate. Coverage baseline auto-raises if it improves; the final commit captures the raised baseline.
4. Push branch; open PR.

## File-by-file checklist (in commit order)

- [ ] `docs/plans/40-nunit-migration.md` — this file
- [ ] `test/Studywise.CLI.E2ETests/` — git rm -r
- [ ] `test/.devproxy/` — git rm -r
- [ ] `docs/deep-dive/cli-e2e-testing-http-mock.md` — git rm
- [ ] `test/Studywise.CLI.UnitTests/.../Studywise.CLI.UnitTests.csproj` — new package set
- [ ] `test/Studywise.CLI.IntegrationTests/.../Studywise.CLI.IntegrationTests.csproj` — new package set, drop dead refs
- [ ] 8 unit test `.cs` files — rewrite attribute + assertion style
- [ ] 1 integration test `.cs` file — rewrite + add `BaseIntegrationTest`
- [ ] `test/Studywise.CLI.UnitTests/.../BaseIntegrationTest.cs` — new
- [ ] `scripts/verify.sh` — comment + filter + scope updates
- [ ] `.github/workflows/ci-fast.yml` — comment update
- [ ] `.github/workflows/ci-full.yml` — drop Dev Proxy, drop API key gating
- [ ] `docs/testing/testing-strategy.md` — full rewrite
- [ ] `docs/architecture/cli-test-architecture.md` — targeted edit
- [ ] `docs/devenv/setup.md` — drop Dev Proxy section
- [ ] `AGENTS.md` — update comment about xunit/Fast-LongRunning
- [ ] `Studywise.Cli.sln` — drop E2E project reference (auto via `dotnet sln remove` after deleting the dir)
- [ ] `.github/coverage-baseline.json` — regenerated by Phase 8 auto-raise
