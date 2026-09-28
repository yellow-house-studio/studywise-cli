# Local CI Validation Scripts

These scripts run CI-equivalent checks locally before you push.

## Prerequisites

- **bash 3.2+** — built in on macOS and Linux.
- **dotnet SDK** matching the version pinned in `global.json` (currently
  `.NET 10.0`).
- **reportgenerator** (the .NET global tool
  `dotnet-reportgenerator-globaltool`) — required by `verify.sh`'s
  coverage-merge phase (Phase 6.5). Install with
  `./scripts/setup-env.sh reportgenerator` or
  `dotnet tool install -g dotnet-reportgenerator-globaltool`. After
  install, `~/.dotnet/tools` may need to be on PATH —
  `export PATH="$HOME/.dotnet/tools:$PATH"`.
- **gitleaks** (defensive secret-scan layer, Phase 5a) — optional.
  When installed, `verify.sh` scans the working tree for known secret
  patterns and fails the run if any are found. Install with
  `./scripts/setup-env.sh gitleaks` (mac: `brew install gitleaks`,
  Linux: downloads the static binary to `/usr/local/bin`).
- **actionlint** (workflow-file lint, Phase 5b) — optional. When
  installed, `verify.sh` lints `.github/workflows/*.yml,*.yaml` and
  fails the run on malformed workflow syntax. Install with
  `./scripts/setup-env.sh actionlint` (mac: `brew install actionlint`,
  Linux: downloads the v1.7.7 binary).
- **cspell** (source .cs spell-check, Phase 5c) — runs via
  `npx cspell@10` on demand from verify.sh. No separate install:
  cspell is downloaded on first run and cached by npm. The repo-root
  `cspell.json` is the source of truth for the wordlist and ignore
  patterns; `--skip-cspell` opts out per run.
- **Dev Proxy** (for E2E tests) — local devs run it via Dev Proxy
  SDK (`devproxy` binary on PATH). In CI, `ci-full.yml` installs it
  via `dev-proxy-tools/actions/setup@v1`. See
  `docs/devenv/setup.md` for the local setup recipe.
- `STUDYWISE_API_KEY` — needed for E2E tests (the CLI authenticates
  via the `X-Studywise-Api-Key` header). Set it in the shell or in
  `~/.secrets/studywise-cli.env`. `verify.sh --scope=unit` and
  `--scope=integration` skip E2E without a key; `--scope=full` blocks
  pre-flight with a clear fix-it pointer.

## Canonical entry point: `scripts/verify.sh`

The local CI mirror is a single script: `scripts/verify.sh`. It is the
source of truth for both local dev runs and the GitHub Actions
workflows (ci-fast.yml, ci-full.yml, ci-production.yml,
update-baseline-on-merge.yml). Editing the local script and editing the
workflows in tandem stops being necessary — the workflows shell out to
`verify.sh` instead of duplicating its dotnet command sequence.

### Scopes

| Scope          | What runs                                                   | Time  |
|----------------|-------------------------------------------------------------|-------|
| `--scope=unit` | Restore + build + unit tests                                | 1-3m  |
| `--scope=integration` | Restore + build + unit + integration tests            | 2-4m  |
| `--scope=full` | Restore + build + unit + integration + e2e (needs API key) | 5-15m |
| `--scope=fast`  | Same as `--scope=full` (CLI has no Fast/LongRunning split)  | 5-15m |

`--scope=full` and `--scope=fast` are identical in this repo because
xunit (the CLI's test framework) doesn't have the `Category=Fast`
attribute pattern NUnit uses.

### What runs under each scope

| Phase                          | unit | integration | full / fast |
|--------------------------------|------|-------------|-------------|
| 1. Pre-flight (STUDYWISE_API_KEY)| ✅   | ✅ (warn)   | ✅ (block)  |
| 2. Restore                     | ✅   | ✅          | ✅          |
| 3. Build (Release + WarningsAsErrors) | ✅ | ✅     | ✅          |
| 4a. Format (`dotnet format`)   | ✅   | ✅          | ✅          |
| 4b. Analyzers                  | ✅   | ✅          | ✅          |
| 5a. gitleaks (if installed)    | ✅   | ✅          | ✅          |
| 5b. actionlint (if installed)  | ✅   | ✅          | ✅          |
| 5c. cspell                     | ✅   | ✅          | ✅          |
| 5d. Security scan              | ✅   | ✅          | ✅          |
| 6. Unit tests                  | ✅   | ✅          | ✅          |
| 6. Integration tests           |      | ✅          | ✅          |
| 6. E2E tests                   |      |             | ✅ + (if API key) |
| 6.5. Coverage merge (ReportGenerator) | ✅ | ✅       | ✅          |
| 7. Coverage gate                |      |             | ✅          |
| 8. Auto-raise baseline         |      |             | ✅          |
| 9. PR comment (`--post-comment`)| opt  | opt         | opt         |

All skip flags are independent (`--skip-format`, `--skip-analyzers`,
`--skip-gitleaks`, `--skip-actionlint`, `--skip-cspell`,
`--skip-security`, `--skip-e2e`, `--skip-coverage-gate`,
`--no-auto-raise`).

### Error mode

- **Default: fail-fast** — first broken phase short-circuits the run.
  Local devs get the first-broken-thing signal in seconds.
- **`--no-fail-fast`** — collect-all. Continues through every phase,
  exits with the aggregate code at the end. Useful for diagnostic
  sweeps or CI runs that want the full picture.

### Examples

```bash
# The dev inner loop (after a small change).
./scripts/verify.sh --scope=unit

# Pre-push gate (default for the pre-push hook).
./scripts/verify.sh --scope=fast

# Heavy full check — what ci-full.yml runs.
./scripts/verify.sh --scope=full

# Run without the coverage gate (e.g., dropping a feature in a hurry).
./scripts/verify.sh --scope=full --skip-coverage-gate

# Investigate a specific failure — run everything, see everything.
./scripts/verify.sh --scope=full --no-fail-fast

# Skip just the E2E phase (no api key handy).
./scripts/verify.sh --scope=full --skip-e2e

# Wipe TestResults/ and exit 0.
./scripts/verify.sh --clean
```

## Reading a failure

When `verify.sh` fails, the script prints the failing project + failing
test names + first line of each error (parsed from the TRX file under
`TestResults/Verify/`). Every stage writes
`TestResults/<stage>/console.log` (full verbose xunit output,
grep-able) and `TestResults/<stage>/stderr.log` so a failed test is
inspectable without re-running. If the printed summary is insufficient,
file an issue referencing `scripts/lib/test-report.sh` before adding
workarounds — do NOT substitute `dotnet test <one-project>` as a
debugging step.

## Helper scripts

- `scripts/setup-env.sh` — friendly one-shot bootstrap for the
  local dev environment (Node, reportgenerator, gitleaks,
  actionlint). With no args, installs every registered tool. With
  one or more TOOL names, installs only those. `--check` mode exits
  0 if everything's ready, 5 with a per-tool pointer otherwise.
- `scripts/install-ci-pre-push-hook.sh` — installs
  `ci-pre-push-hook.sh` as `.git/hooks/pre-push` so `git push` runs
  `verify.sh --scope=fast` before allowing the push. Idempotent;
  `--uninstall` removes it.
- `scripts/lib/test-report.sh` — sourced by `verify.sh`; not run
  directly. Provides TRX summary, stage wrapping, sticky PR-comment
  posting helpers.
- `scripts/ci-pre-push-hook.sh` — body of the pre-push hook. Don't run
  directly; install via `install-ci-pre-push-hook.sh`.