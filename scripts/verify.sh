#!/usr/bin/env bash
# verify.sh — unified local CI script for Studywise CLI.
#
# PURPOSE: Single entry point for the local CI surface. Replaces the
# previous split between `ci-fast-local.sh` (dev inner loop) and
# `ci-full-local.sh` (pre-push / deploy-verify mirror). The script is
# the local mirror of `.github/workflows/ci-fast.yml`,
# `ci-full.yml`, `ci-production.yml`, and `update-baseline-on-merge.yml`
# — single source of truth for both local dev and CI.
#
# Phases (each independently skippable):
#   1. Pre-flight (STUDYWISE_API_KEY for E2E tests)
#   2. Restore
#   3. Build (Release, TreatWarningsAsErrors=true, EnforceXmlDocs=true)
#   4. Format + analyzers (+ gitleaks + actionlint + cspell if installed)
#   5. Security scan (`dotnet list package --vulnerable`)
#   6. Tests (unit → integration → e2e)
#   6.5. Coverage merge (ReportGenerator; per-test-project cobertura XMLs
#        → one deduplicated Cobertura.xml at $RESULTS_DIR/coverage/Cobertura.xml)
#   7. Coverage gate (per-assembly drop tolerance + new-file ≥80%)
#   8. Baseline auto-raise (writes raised per-assembly numbers to
#      .github/coverage-baseline.json when coverage improves)
#   9. PR comment (only with --post-comment; off by default)
#
# Error mode:
#   - Default: fail-fast. Stops on the first phase that fails so local
#     devs get the first-broken-thing signal in seconds.
#   - --no-fail-fast: collect-all. Continues through every phase and
#     surfaces every failure in the final summary. Use for diagnostic
#     sweeps or CI runs where you want the full picture.
#
# Scope (--scope flag, default=full):
#   - full:        all 3 test projects (unit + integration + e2e)
#   - fast:        same as full (CLI is small enough that there is no
#                  Fast/LongRunning category split — see xunit, not NUnit)
#   - unit:        only unit tests
#   - integration: unit + integration (no e2e)
#
# Coverage gate + auto-raise only run under --scope=full (otherwise
# the coverage numbers reflect only a subset of the suite and the
# gate's "drop vs baseline" comparison is meaningless).
#
# NOT mirrored (intentionally):
#   - Publish + NuGet pack + GitHub Release (CD concern, owned by
#     ci-production.yml, not a CI gate).
#   - Dev Proxy setup — CI installs it via dev-proxy-tools/actions/setup@v1
#     in ci-full.yml; locally the E2E tests skip cleanly when the
#     proxy URL env var is unset. Local devs run Dev Proxy by hand
#     per docs/devenv/setup.md when iterating on E2E tests.
#
# Required environment:
#   STUDYWISE_API_KEY  — needed for E2E tests (the CLI authenticates
#                         via the X-Studywise-Api-Key header). The
#                         script's pre-flight reads it from the shell
#                         or ~/.secrets/studywise-cli.env and exits 2
#                         with a fix-it pointer if missing under
#                         --scope=full or --scope=fast. Under
#                         --scope=unit or --scope=integration, the
#                         E2E stage is skipped without the key.
#
# Tooling:
#   reportgenerator — the .NET global tool `dotnet-reportgenerator-globaltool`
#                    (install: `dotnet tool install -g dotnet-reportgenerator-globaltool`).
#                    Phase 6.5 uses it to merge the per-test-project
#                    cobertura XMLs into one deduplicated Cobertura.xml.
#                    If reportgenerator isn't installed, Phase 6.5
#                    fails fast with a clear fix-it pointer. The
#                    coverage gate and auto-raise both depend on the
#                    merged file, so a missing reportgenerator breaks
#                    the coverage surface end-to-end.
#
# Prerequisites:
#   - .NET SDK matching the global.json pin.
#   - Optional: gitleaks (secret scan), actionlint (workflow lint).
#     Both are auto-installed by scripts/setup-env.sh.
#   - Optional: reportgenerator. Install via scripts/setup-env.sh
#     reportgenerator.
#
# IMPORTANT — NuGet auth pattern:
#   The CLI repo has no private NuGet feeds — all dependencies are on
#   the public NuGet gallery and the `Studywise.*` packages on the
#   YHS public GitHub Packages feed. `dotnet restore` uses the
#   default config hierarchy; no `--configfile` override needed.
#
# Usage:
#   ./scripts/verify.sh                          # --scope=full, fail-fast
#   ./scripts/verify.sh --scope=fast             # dev inner loop (== full here)
#   ./scripts/verify.sh --scope=unit             # unit tests only
#   ./scripts/verify.sh --scope=integration      # unit + integration (no e2e)
#   ./scripts/verify.sh --no-fail-fast           # collect all failures
#   ./scripts/verify.sh --skip-e2e               # skip E2E tests (no api key needed)
#   ./scripts/verify.sh --skip-security          # skip the vulnerable-package scan
#   ./scripts/verify.sh --skip-coverage-gate     # run tests but don't gate
#   ./scripts/verify.sh --skip-format --skip-analyzers
#   ./scripts/verify.sh --no-auto-raise          # don't update the baseline file
#   ./scripts/verify.sh --post-comment           # post a sticky comment to the PR on success
#   ./scripts/verify.sh --post-comment=always    # post a sticky comment on success OR failure
#   ./scripts/verify.sh --verbose                # restore --verbosity normal for dotnet test
#   ./scripts/verify.sh --clean                  # remove TestResults/ and exit 0
#   ./scripts/verify.sh --help
#
# Exit codes:
#   0   all selected checks passed
#   1   one or more checks failed
#   2   invalid arguments / missing prerequisite

set -uo pipefail

DOTNET="${DOTNET:-dotnet}"

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
SLN="$REPO_ROOT/Studywise.Cli.sln"

# ─── Run-state tracking ────────────────────────────────────────────────
# Used by write_summary_json at the end of the run to build the
# structured summary.json. We use a newline-separated string of
# `name=status` lines (NOT a bash associative array) for portability:
# macOS ships bash 3.2.57 which doesn't support `declare -A`. Each
# phase is initialised to "skipped" (the phase didn't run — its
# --skip-* flag was set, or its scope doesn't include it). Phases
# that actually run update their line via set_phase_status below.
PHASE_LIST="preflight=skipped
restore=skipped
build=skipped
format=skipped
analyzers=skipped
gitleaks=skipped
actionlint=skipped
cspell=skipped
security=skipped
unit_tests=skipped
integration_tests=skipped
e2e_tests=skipped
coverage_gate=skipped"

# Update the `name=status` line for a phase in PHASE_LIST. Works on
# bash 3.2 (no associative arrays, no process substitution tricks).
set_phase_status() {
    local name="$1" new_status="$2"
    # Use awk to do the in-place replacement portably.
    PHASE_LIST="$(printf '%s\n' "$PHASE_LIST" | awk -v n="$name" -v s="$new_status" '
        BEGIN { FS = "=" }
        $1 == n { print n "=" s; next }
        { print }
    ')"
}

STARTED_AT="$(date -Iseconds 2>/dev/null || date)"

# Capture the head SHA + commit message at script start (before the
# git working tree changes from a baseline auto-raise write).
HEAD_SHA="$(git rev-parse HEAD 2>/dev/null || echo unknown)"
HEAD_MESSAGE="$(git log -1 --pretty=format:%s 2>/dev/null || echo unknown)"

# ─── Defaults ───────────────────────────────────────────────────────────
SCOPE="full"
FAIL_FAST=1
VERBOSE=0

RUN_RESTORE=1
RUN_BUILD=1
RUN_FORMAT=1
RUN_ANALYZERS=1
RUN_GITLEAKS=1
RUN_ACTIONLINT=1
RUN_CSPELL=1
RUN_SECURITY=1
RUN_TESTS=1
RUN_E2E=1
RUN_COVERAGE_GATE=1
RUN_AUTO_RAISE=1
RUN_PR_COMMENT=0
PR_COMMENT_ALWAYS=0
RUN_CLEAN_ONLY=0

OVERALL_RC=0

# ─── Arg parsing ────────────────────────────────────────────────────────
usage() {
    cat <<EOF
Usage: $0 [--scope=<s>] [--no-fail-fast] [--skip-<phase>...] [--post-comment[=always]] [--no-auto-raise] [--verbose] [--clean] [--help]

Unified local CI script for Studywise CLI.

Scope (default: full):
  --scope=fast         All 3 test projects. CLI is small enough that there's
                       no Fast/LongRunning category split; --scope=fast is
                       identical to --scope=full here. ~3-8 min.
  --scope=full         All 3 test projects (unit + integration + e2e).
                       Mirrors ci-full.yml "build-and-test" job. ~5-15 min.
  --scope=unit         Unit tests only.
  --scope=integration  Unit + integration (no e2e). Useful when Dev Proxy /
                       STUDYWISE_API_KEY aren't available locally.

Error mode:
  --no-fail-fast       Collect all phase failures; exit at the end with aggregate code.
                       Default is fail-fast (stop on first broken phase).

Phase toggles (each skip flag is independent):
  --skip-format        Skip 'dotnet format --verify-no-changes'.
  --skip-analyzers     Skip the .NET analyzer build (security/thread-safety analyzers).
  --skip-gitleaks      Skip the secret-scan phase (gitleaks). Must be installed
                       via './scripts/setup-env.sh gitleaks'; if missing,
                       the phase is skipped with a clear message regardless.
  --skip-actionlint    Skip the workflow-lint phase (actionlint). Same
                       install requirement as --skip-gitleaks.
  --skip-cspell        Skip the source .cs spell-check (cspell). Runs via
                       \`npx cspell@10\` — no separate install; cspell is
                       downloaded on first invocation and cached.
  --skip-security      Skip 'dotnet list package --vulnerable'.
  --skip-e2e           Skip the E2E test phase (handy when STUDYWISE_API_KEY
                       isn't available; equivalent to --scope=integration).
  --skip-coverage-gate Run tests with coverage but don't enforce the baseline gate.
  --no-auto-raise      Don't update .github/coverage-baseline.json at end of run.
  --post-comment       Post a sticky PR comment with the run results on success.
  --post-comment=always  Post a sticky PR comment on success OR failure
                         (use for the dev-reviewer handoff case).

Verbosity:
  --verbose            Use --verbosity normal for 'dotnet test' invocations
                       instead of minimal. The verbose xunit console output
                       is still captured to TestResults/Verify/<stage>/console.log
                       via run_stage() — --verbose just controls whether the
                       dotnet output itself is verbose. No-op for non-test
                       phases (restore, build, format, coverage gate).

Maintenance:
  --clean              Remove TestResults/ and exit 0. Use after a run to wipe
                       stale TRX/coverage files before re-running.

Exit codes: 0=all passed, 1=one or more failed, 2=invalid args / missing prereq.
EOF
}

while [[ $# -gt 0 ]]; do
    case "$1" in
        --scope=*)
            SCOPE="${1#--scope=}"
            case "$SCOPE" in
                fast|full|unit|integration) ;;
                *) echo "ERROR: --scope must be one of fast|full|unit|integration (got: $SCOPE)" >&2; usage >&2; exit 2 ;;
            esac
            ;;
        --no-fail-fast)     FAIL_FAST=0 ;;
        --skip-format)      RUN_FORMAT=0 ;;
        --skip-analyzers)   RUN_ANALYZERS=0 ;;
        --skip-gitleaks)    RUN_GITLEAKS=0 ;;
        --skip-actionlint)  RUN_ACTIONLINT=0 ;;
        --skip-cspell)      RUN_CSPELL=0 ;;
        --skip-security)    RUN_SECURITY=0 ;;
        --skip-e2e)         RUN_E2E=0 ;;
        --skip-coverage-gate) RUN_COVERAGE_GATE=0 ;;
        --no-auto-raise)    RUN_AUTO_RAISE=0 ;;
        --post-comment)     RUN_PR_COMMENT=1; PR_COMMENT_ALWAYS=0 ;;
        --post-comment=always|--post-comment-always)
                           RUN_PR_COMMENT=1; PR_COMMENT_ALWAYS=1 ;;
        --verbose)          VERBOSE=1 ;;
        --clean)            RUN_CLEAN_ONLY=1 ;;
        --help|-h)          usage; exit 0 ;;
        *)
            echo "ERROR: unknown flag: $1" >&2
            usage >&2
            exit 2
            ;;
    esac
    shift
done

# ─── Setup ──────────────────────────────────────────────────────────────
cd "$REPO_ROOT"

if [[ ! -f "$SLN" ]]; then
    echo "[verify] ❌ Studywise.Cli.sln not found at $SLN" >&2
    exit 1
fi

# Source the shared TRX-summary + stage helpers. LOG_PREFIX drives the
# `[verify]` tag on every helper-emitted line.
LOG_PREFIX="verify"
# shellcheck source=lib/test-report.sh
source "$SCRIPT_DIR/lib/test-report.sh"

# `--clean` short-circuits before pre-flights so cleanup works in any
# state — including when the toolchain isn't fully set up.
if [[ "$RUN_CLEAN_ONLY" == "1" ]]; then
    TARGET="$REPO_ROOT/TestResults"
    if [[ -d "$TARGET" ]]; then
        rm -rf "$TARGET"
        echo "[verify] ✅ Removed $TARGET"
    else
        echo "[verify] ⚠️  No $TARGET to remove"
    fi
    exit 0
fi

# Coverage gate + auto-raise only meaningful under --scope=full. Under
# other scopes the coverage numbers reflect only a subset of the suite
# and the per-assembly "drop vs baseline" comparison is meaningless.
if [[ "$SCOPE" != "full" ]]; then
    if [[ "$RUN_COVERAGE_GATE" == "1" ]]; then
        echo "[verify] ⚠️  Coverage gate is disabled under --scope=$SCOPE (only meaningful under --scope=full)"
        RUN_COVERAGE_GATE=0
    fi
    if [[ "$RUN_AUTO_RAISE" == "1" ]]; then
        echo "[verify] ⚠️  Baseline auto-raise is disabled under --scope=$SCOPE (only meaningful under --scope=full)"
        RUN_AUTO_RAISE=0
    fi
fi

# Run a phase: if it fails and FAIL_FAST is set, exit 1. If not, set
# OVERALL_RC=1 and continue. $1 is the phase name (for log output AND
# for PHASE_LIST tracking); the rest is the command + args to run.
# Pass an empty second arg when you just want to record an outcome
# without running a command (used by pre-flight, security scan, and
# coverage gate which manage their own exit codes).
run_phase() {
    local phase_name="$1"
    shift
    if [[ "$#" -eq 0 ]]; then
        # No command to run; just record current PHASE_LIST entry
        # (the caller has already set it before calling run_phase).
        return 0
    fi
    if "$@"; then
        echo "[verify] ✅ $phase_name"
        set_phase_status "$phase_name" passed
    else
        local rc=$?
        echo "[verify] ❌ $phase_name failed (rc=$rc)" >&2
        set_phase_status "$phase_name" failed
        if [[ "$FAIL_FAST" == "1" ]]; then
            exit "$rc"
        else
            OVERALL_RC=1
        fi
    fi
}

# ─── Pre-flight: STUDYWISE_API_KEY for E2E tests ───────────────────────
# The E2E tests in test/Studywise.CLI.E2ETests hit the real Studywise
# API (mocked by Dev Proxy in CI; locally you'd point at staging). They
# authenticate via the X-Studywise-Api-Key header, set from the
# STUDYWISE_API_KEY environment variable. Without a key, the E2E tests
# fail with a cryptic 401 deep in the test runner.
#
# Only blocks under --scope=full / --scope=fast (E2E phase runs).
# Under --scope=unit / --scope=integration the E2E phase is skipped
# and the missing key prints a one-line warning, not a fail.
#
# Reads STUDYWISE_API_KEY from the shell first; falls back to
# ~/.secrets/studywise-cli.env (one-line `STUDYWISE_API_KEY=...` or
# `export STUDYWISE_API_KEY=...`).
preflight_api_key() {
    local key="${STUDYWISE_API_KEY:-}"
    local env_file="$HOME/.secrets/studywise-cli.env"

    if [[ -z "$key" && -f "$env_file" ]]; then
        key=$(grep -E '^[[:space:]]*(export[[:space:]]+)?STUDYWISE_API_KEY=' "$env_file" \
              | head -1 \
              | sed -E 's/^[[:space:]]*(export[[:space:]]+)?STUDYWISE_API_KEY=["'\'']?//; s/["'\'']?$//')
    fi

    if [[ -z "$key" ]]; then
        echo "[verify] ❌ STUDYWISE_API_KEY is not set." >&2
        echo "    E2E tests in test/Studywise.CLI.E2ETests authenticate against" >&2
        echo "    the Studywise API via X-Studywise-Api-Key header and need a key." >&2
        echo "" >&2
        echo "    To fix:" >&2
        echo "      export STUDYWISE_API_KEY=<your-key>      # any shell session" >&2
        echo "      # or, for persistence across shells:" >&2
        echo "      echo 'export STUDYWISE_API_KEY=<your-key>' >> ~/.secrets/studywise-cli.env" >&2
        echo "" >&2
        echo "    To skip E2E without a key:" >&2
        echo "      $0 --skip-e2e            # or --scope=integration / --scope=unit" >&2
        echo "" >&2
        echo "    See docs/devenv/setup.md for the full setup recipe." >&2
        return 2
    fi

    export STUDYWISE_API_KEY="$key"
    return 0
}

DOTNET_RESTORE_FLAGS=(--verbosity minimal)

DOTNET_BUILD_FLAGS=(
    --configuration Release
    --no-restore
    --verbosity minimal
    "/p:TreatWarningsAsErrors=true"
    "/p:WarningsNotAsErrors=AD0001"
    "/p:EnforceXmlDocs=true"
)

# Verbosity for `dotnet test` invocations is controlled separately so
# --verbose can opt into --verbosity normal without affecting build /
# restore (test scope only). Default is `minimal` to match ci-fast.yml /
# ci-full.yml.
if [[ "$VERBOSE" == "1" ]]; then
    TEST_VERBOSITY_FLAG=(--verbosity normal)
else
    TEST_VERBOSITY_FLAG=(--verbosity minimal)
fi

DOTNET_TEST_FLAGS=(
    --configuration Release
    --no-build
    "${TEST_VERBOSITY_FLAG[@]}"
)

RESULTS_DIR="$REPO_ROOT/TestResults/Verify"
mkdir -p \
    "$RESULTS_DIR/Restore" \
    "$RESULTS_DIR/Build" \
    "$RESULTS_DIR/Format" \
    "$RESULTS_DIR/Analyzers" \
    "$RESULTS_DIR/Security" \
    "$RESULTS_DIR/Unit" \
    "$RESULTS_DIR/Integration" \
    "$RESULTS_DIR/E2E" \
    "$RESULTS_DIR/Coverage"

# ─── Phase 1: Pre-flight ────────────────────────────────────────────────
# CLI's pre-flight is much lighter than api's: no NuGet auth (CLI has
# no private feeds), no Stripe (no payments in CLI), no Azurite (no
# blob storage in CLI), no Node/Azurite check (Node is only a soft
# dependency for `npx cspell@10` in Phase 5d, and the cspell phase
# self-skips if `npx` is missing).
#
# Only STUDYWISE_API_KEY is gating — and only under scopes that
# actually run E2E tests. Under --scope=unit / --scope=integration we
# still try to find the key (so the summary.json records whether one
# was available), but a missing key is a warning, not a fail.
echo "[verify] === Phase 1: Pre-flight ==="
PRE_OK=1
case "$SCOPE" in
    full|fast)
        if ! preflight_api_key; then
            PRE_OK=0
        fi
        ;;
    unit|integration)
        if [[ -z "${STUDYWISE_API_KEY:-}" ]] && [[ ! -f "$HOME/.secrets/studywise-cli.env" ]]; then
            echo "[verify] ⚠️  STUDYWISE_API_KEY not set — E2E phase will be skipped if reached."
            set_phase_status preflight passed
        else
            preflight_api_key || PRE_OK=0
        fi
        ;;
esac
if [[ "$PRE_OK" == "1" ]]; then
    echo "[verify] ✅ Pre-flight"
    set_phase_status preflight passed
else
    set_phase_status preflight failed
    if [[ "$FAIL_FAST" == "1" ]]; then
        exit 2
    else
        OVERALL_RC=1
    fi
fi

# ─── Phase 2: Restore ──────────────────────────────────────────────────
echo "[verify] === Phase 2: Restore ==="
RESTORE_LOG="$(mktemp -t verify-restore.XXXXXX.log)"
phase_restore() {
    if ! "$DOTNET" restore "$SLN" "${DOTNET_RESTORE_FLAGS[@]}" >"$RESTORE_LOG" 2>&1; then
        if grep -q "401 (Unauthorized)" "$RESTORE_LOG"; then
            # The CLI only consumes the YHS public GitHub Packages
            # feed, which doesn't need auth for the public Studywise.*
            # packages. A 401 here is unusual — surface it with a
            # generic NuGet-credential pointer.
            echo "[verify] ❌ Restore failed with NU1301: 401 Unauthorized." >&2
            echo "    A NuGet source rejected the credentials in" >&2
            echo "    ~/.nuget/NuGet/NuGet.Config. This is unusual for the CLI" >&2
            echo "    repo (only public NuGet feeds are configured). Check" >&2
            echo "    that no user-level private feed is configured with a" >&2
            echo "    stale PAT." >&2
        else
            echo "[verify] ❌ Restore failed — see $RESTORE_LOG" >&2
        fi
        rm -f "$RESTORE_LOG"
        return 1
    fi
    rm -f "$RESTORE_LOG"
    return 0
}
run_phase "Restore" phase_restore

# ─── Phase 3: Build ─────────────────────────────────────────────────────
echo "[verify] === Phase 3: Build (Release) ==="
run_phase "Build" \
    run_stage "Build (Release, TreatWarningsAsErrors=true)" "$RESULTS_DIR/Build" --no-trx \
        "$DOTNET" build "$SLN" "${DOTNET_BUILD_FLAGS[@]}"

# ─── Phase 4a: Format ───────────────────────────────────────────────────
if [[ "$RUN_FORMAT" == "1" ]]; then
    echo "[verify] === Phase 4a: Code formatting ==="
    run_phase "Format" \
        run_stage "Code formatting check" "$RESULTS_DIR/Format" --no-trx \
            "$DOTNET" format "$SLN" --verify-no-changes --verbosity minimal
fi

# ─── Phase 4b: Analyzers ────────────────────────────────────────────────
if [[ "$RUN_ANALYZERS" == "1" ]]; then
    echo "[verify] === Phase 4b: Analyzers ==="
    run_phase "Analyzers" \
        run_stage "Security and thread safety analyzers" "$RESULTS_DIR/Analyzers" --no-trx \
            "$DOTNET" build "$SLN" -c Release --no-restore \
                "/p:EnableNETAnalyzers=true" \
                "/p:TreatWarningsAsErrors=true" \
                "/p:WarningsNotAsErrors=AD0001"
fi

# ─── Phase 5a: Gitleaks (defensive secret-scan) ─────────────────────────
# Mirrors ci-fast.yml's defensive-layer convention. gitleaks inspects
# the working tree for known secret patterns (AWS keys, GitHub tokens,
# Stripe live keys, etc.) and fails the run if any are found.
#
# Two-pass pattern:
#   1. Generate a baseline from current HEAD (captures all known leaks in
#      this branch). Allowed to fail; `|| true` so a dirty tree doesn't
#      double-fail.
#   2. Scan with --baseline-path; flags only NEW leaks introduced beyond
#      the baseline. This is the actual gate.
#
# If gitleaks isn't installed, skip with a clear install pointer rather
# than failing — defensive layers shouldn't block devs who haven't
# bootstrapped the optional tooling. The flag --skip-gitleaks is the
# explicit opt-out (e.g. when running on a CI runner without gitleaks).
if [[ "$RUN_GITLEAKS" == "1" ]]; then
    echo "[verify] === Phase 5a: Secret scan (gitleaks) ==="
    if ! command -v gitleaks >/dev/null 2>&1; then
        echo "[verify] ⚠️  gitleaks not found on PATH; skipping phase."
        echo "    Install: ./scripts/setup-env.sh gitleaks"
        echo "    Or skip explicitly: --skip-gitleaks"
        set_phase_status gitleaks skipped
    else
        SECRET_DIR="$RESULTS_DIR/SecretScan"
        mkdir -p "$SECRET_DIR"
        BASELINE="$SECRET_DIR/gitleaks-baseline.json"
        # (1) baseline — captures all currently-known leaks in the
        # branch. The follow-up scan only flags NEW leaks beyond this set.
        gitleaks detect --redact --source . \
            -f json -r "$BASELINE" 2>/dev/null || true
        run_phase "gitleaks" \
            run_stage "Secret scan (gitleaks)" "$SECRET_DIR" --no-trx \
                gitleaks detect --redact --source . \
                    --baseline-path "$BASELINE" --verbose
    fi
fi

# ─── Phase 5b: actionlint (workflow-file lint) ─────────────────────────
# Mirrors ci-fast.yml's workflow-file lint. Catches malformed workflow
# steps (bad `if:` expressions, missing matrix corners, incorrect
# `uses:` versions, etc.) before push — pre-push the dev sees the same
# lint output as CI, instead of learning about it via a red PR.
#
# Runs actionlint on every .github/workflows/*.yml/*.yaml every invocation
# (no gating on which files changed). Cheap (<2s on this repo) and the
# "always-lint" behaviour catches pre-existing issues that get surfaced by
# later refactors; the --skip-actionlint flag is the explicit opt-out.
if [[ "$RUN_ACTIONLINT" == "1" ]]; then
    echo "[verify] === Phase 5b: Workflow lint (actionlint) ==="
    if ! command -v actionlint >/dev/null 2>&1; then
        echo "[verify] ⚠️  actionlint not found on PATH; skipping phase."
        echo "    Install: ./scripts/setup-env.sh actionlint"
        echo "    Or skip explicitly: --skip-actionlint"
        set_phase_status actionlint skipped
    else
        ACTIONLINT_DIR="$RESULTS_DIR/Actionlint"
        mkdir -p "$ACTIONLINT_DIR"
        # Expand glob with nullglob so missing files produce an empty
        # (set -u-safe) list rather than the literal pattern on disk.
        shopt -s nullglob
        WORKFLOW_FILES=(.github/workflows/*.yml .github/workflows/*.yaml)
        shopt -u nullglob
        if [[ ${#WORKFLOW_FILES[@]} -eq 0 ]]; then
            echo "[verify] ⚠️  No workflow files found under .github/workflows/"
            set_phase_status actionlint skipped
        else
            run_phase "actionlint" \
                run_stage "Workflow lint (actionlint)" "$ACTIONLINT_DIR" --no-trx \
                    actionlint -color "${WORKFLOW_FILES[@]}"
        fi
    fi
fi

# ─── Phase 5c: cspell (source .cs spell check) ────────────────────────
# Mirrors the api reference CI spell-check. Scans src/**/*.cs against
# the cspell.json wordlist + ignore-patterns at repo root.
#
# No separate install: cspell runs via `npx cspell@10`, which downloads
# the pinned v10.x on first invocation (npm cache makes subsequent runs
# free). Node is only a soft dependency for verify.sh's cspell phase
# (no other phase needs npx), so missing npx auto-skips this phase
# with a clear pointer rather than failing the run.
#
# If cspell finds unrecognised words, the run fails fast (default) or
# aggregates (--no-fail-fast). The error output is grep-able for the
# offending file:line.
if [[ "$RUN_CSPELL" == "1" ]]; then
    echo "[verify] === Phase 5c: Source spell-check (cspell) ==="
    if [[ ! -f "$REPO_ROOT/cspell.json" ]]; then
        echo "[verify] ⚠️  cspell.json not found at repo root; skipping phase."
        echo "    Restore the file or skip explicitly: --skip-cspell"
        set_phase_status cspell skipped
    elif ! command -v npx >/dev/null 2>&1; then
        echo "[verify] ⚠️  npx not found on PATH; skipping phase."
        echo "    Install Node via ./scripts/setup-env.sh node"
        echo "    Or skip explicitly: --skip-cspell"
        set_phase_status cspell skipped
    else
        CSPELL_DIR="$RESULTS_DIR/Cspell"
        mkdir -p "$CSPELL_DIR"
        run_phase "cspell" \
            run_stage "Source spell-check (cspell)" "$CSPELL_DIR" --no-trx \
                npx --yes cspell@10 --config "$REPO_ROOT/cspell.json" \
                    --no-progress --no-summary \
                    "src/**/*.cs"
    fi
fi

# ─── Phase 5d: Security scan ─────────────────────────────────────────────
if [[ "$RUN_SECURITY" == "1" ]]; then
    echo "[verify] === Phase 5d: Security scan ==="
    SECURITY_DIR="$RESULTS_DIR/Security"
    mkdir -p "$SECURITY_DIR"
    begin_stage "Security scan (dotnet list package --vulnerable --include-transitive)"
    SCAN_OUTPUT=$("$DOTNET" list "$SLN" package --vulnerable --include-transitive 2>&1 | tee "$SECURITY_DIR/console.log" || true)
    end_stage "Security scan"
    if echo "$SCAN_OUTPUT" | grep -qE "High|Critical"; then
        echo "[verify] ❌ High or Critical vulnerabilities found:" >&2
        echo "$SCAN_OUTPUT" | grep -E "High|Critical" >&2
        set_phase_status security failed
        if [[ "$FAIL_FAST" == "1" ]]; then
            exit 1
        else
            OVERALL_RC=1
        fi
    elif echo "$SCAN_OUTPUT" | grep -q "has the following vulnerable packages"; then
        echo "[verify] ⚠️  Moderate/Low vulnerabilities (non-blocking):"
        echo "$SCAN_OUTPUT"
        set_phase_status security passed
    else
        echo "[verify] ✅ Security scan: no vulnerable packages"
        set_phase_status security passed
    fi
fi

# ─── Phase 6: Tests ─────────────────────────────────────────────────────
# CLI has 3 test projects: Unit, Integration, E2E. No application/
# api split (the api repo has those because of its BoundedContext
# architecture; CLI is a single-process CLI tool). No Category=Fast
# filter — xunit doesn't have the Fast/LongRunning attribute pattern
# NUnit uses, so the --scope=fast filter would always match the full
# set. CLI's --scope=fast == --scope=full.
if [[ "$RUN_TESTS" == "1" ]]; then
    DOTNET_TEST_FLAGS_COLLECT=("${DOTNET_TEST_FLAGS[@]}" --collect:"XPlat Code Coverage")

    case "$SCOPE" in
        full|fast)
            RUN_UNIT=1; RUN_INT=1; RUN_E2E_EFFECTIVE=$RUN_E2E
            ;;
        unit)
            RUN_UNIT=1; RUN_INT=0; RUN_E2E_EFFECTIVE=0
            ;;
        integration)
            RUN_UNIT=1; RUN_INT=1; RUN_E2E_EFFECTIVE=0
            ;;
        *)
            echo "[verify] ❌ Unknown scope: $SCOPE" >&2
            exit 2
            ;;
    esac

    # Unit tests
    TEST_PROJ="$REPO_ROOT/test/Studywise.CLI.UnitTests/Studywise.Cli.UnitTests/Studywise.CLI.UnitTests.csproj"
    if [[ ! -f "$TEST_PROJ" ]]; then
        echo "[verify] ❌ Unit test project not found: $TEST_PROJ" >&2
        OVERALL_RC=1
    elif [[ "$RUN_UNIT" == "1" ]]; then
        UNIT_FLAGS=("${DOTNET_TEST_FLAGS_COLLECT[@]}")
        run_phase "unit_tests" \
            run_stage "Unit tests" "$RESULTS_DIR/Unit" \
                "$DOTNET" test "$TEST_PROJ" "${UNIT_FLAGS[@]}" \
                    --logger "trx;LogFileName=Studywise.CLI.UnitTests.trx" \
                    --results-directory "$RESULTS_DIR/Unit"
    fi

    # Integration tests
    if [[ "$RUN_INT" == "1" ]]; then
        TEST_PROJ="$REPO_ROOT/test/Studywise.CLI.IntegrationTests/Studywise.Cli.IntegrationTests/Studywise.CLI.IntegrationTests.csproj"
        if [[ ! -f "$TEST_PROJ" ]]; then
            echo "[verify] ❌ Integration test project not found: $TEST_PROJ" >&2
            OVERALL_RC=1
        else
            INT_FLAGS=("${DOTNET_TEST_FLAGS_COLLECT[@]}")
            run_phase "integration_tests" \
                run_stage "Integration tests" "$RESULTS_DIR/Integration" \
                    "$DOTNET" test "$TEST_PROJ" "${INT_FLAGS[@]}" \
                        --logger "trx;LogFileName=Studywise.CLI.IntegrationTests.trx" \
                        --results-directory "$RESULTS_DIR/Integration"
        fi
    fi

    # E2E tests (full/fast scopes only, AND requires STUDYWISE_API_KEY).
    # If the key isn't present we record a warning and skip rather than
    # fail — local devs running --scope=integration shouldn't have to
    # set up the API key just to test the diagnostic checks.
    if [[ "$RUN_E2E_EFFECTIVE" == "1" ]]; then
        TEST_PROJ="$REPO_ROOT/test/Studywise.CLI.E2ETests/Studywise.CLI.E2ETests.csproj"
        if [[ ! -f "$TEST_PROJ" ]]; then
            echo "[verify] ❌ E2E test project not found: $TEST_PROJ" >&2
            OVERALL_RC=1
        elif [[ -z "${STUDYWISE_API_KEY:-}" ]]; then
            echo "[verify] ⚠️  STUDYWISE_API_KEY is not set — skipping E2E tests." >&2
            echo "    To run E2E locally: export STUDYWISE_API_KEY=<key> (and" >&2
            echo "    usually STUDYWISE_API_BASE_URL=http://localhost:8000 if using Dev Proxy)." >&2
            set_phase_status e2e_tests skipped
        else
            E2E_FLAGS=("${DOTNET_TEST_FLAGS_COLLECT[@]}")
            run_phase "e2e_tests" \
                run_stage "E2E tests" "$RESULTS_DIR/E2E" \
                    "$DOTNET" test "$TEST_PROJ" "${E2E_FLAGS[@]}" \
                        --logger "trx;LogFileName=Studywise.CLI.E2ETests.trx" \
                        --results-directory "$RESULTS_DIR/E2E"
        fi
    fi
fi

# ─── Phase 6.5: Coverage merge ───────────────────────────────────────────
# Each test stage emitted its own `coverage.cobertura.xml` (one per test
# project). To get a single union of coverage across all stages (a line
# covered if ANY test project hit it), we use ReportGenerator to merge.
# ReportGenerator handles the per-class line deduplication that a naive
# XML walker would miss (each line is emitted twice per class: once under
# `<class>/<lines>` and once under `<class>/<methods>/<method>/<lines>`).
#
# The merged Cobertura.xml is written to $RESULTS_DIR/coverage/Cobertura.xml.
# Phases 7 (gate) and 8 (auto-raise) read it; the PR comment reads from
# the same file for per-file coverage data on files in the PR diff.
#
# Only runs when tests have actually run (otherwise no XMLs to merge).
# Always runs (cost ~3-6 seconds) even under scopes that don't gate —
# keeps the script deterministic and avoids branching on whether to
# merge.
COVERAGE_MERGED=""
COVERAGE_DIR="$RESULTS_DIR/coverage"
COVERAGE_XMLS=$(find "$RESULTS_DIR" -name "coverage.cobertura.xml" -not -path "*/coverage/*" 2>/dev/null | wc -l | tr -d ' ')
if [[ "$COVERAGE_XMLS" -gt 0 ]]; then
    if ! command -v reportgenerator >/dev/null 2>&1; then
        echo "[verify] ❌ 'reportgenerator' not found in PATH." >&2
        echo "    Coverage merge needs the dotnet-reportgenerator-globaltool." >&2
        echo "    Install with: dotnet tool install -g dotnet-reportgenerator-globaltool" >&2
        echo "    See docs/devenv/setup.md for the full setup." >&2
        if [[ "$FAIL_FAST" == "1" ]]; then
            exit 1
        else
            OVERALL_RC=1
        fi
    else
        echo "[verify] === Phase 6.5: Coverage merge ==="
        mkdir -p "$COVERAGE_DIR"
        MERGE_LOG="$COVERAGE_DIR/merge.log"
        # ReportGenerator names the merged file after the report type
        # (`-reporttypes:Cobertura` → `Cobertura.xml`).
        COVERAGE_MERGED="$COVERAGE_DIR/Cobertura.xml"
        # ReportGenerator merges all *.cobertura.xml under the results
        # dir into one deduplicated Cobertura.xml. The `-assemblyfilters:+studywise*`
        # excludes third-party assemblies (Microsoft.*, System.*, etc.) that
        # Coverlet still emits — we only want our assemblies in the gate.
        # CLI's assembly is named `studywise` (lowercase, per the
        # <AssemblyName>studywise</AssemblyName> in Studywise.Cli.csproj). The
        # asterisk matches future `studywise.X` sub-assemblies without a dot
        # (the `+Studywise.*` form that api uses requires a literal dot,
        # which the lowercase assembly name doesn't have).
        if reportgenerator \
                -reports:"$RESULTS_DIR/**/coverage.cobertura.xml" \
                -targetdir:"$COVERAGE_DIR" \
                -reporttypes:"Cobertura" \
                -assemblyfilters:"+studywise*" \
                >"$MERGE_LOG" 2>&1; then
            if [[ -f "$COVERAGE_MERGED" ]]; then
                # Strip trailing newlines from grep output (otherwise awk's -v
                # flag parsing sees '0.49\n' and the percentage prints empty).
                OVERALL_RATE=$(grep -o 'line-rate="[0-9.]*"' "$COVERAGE_MERGED" | head -1 | grep -o '[0-9.]*' | tr -d '\n' || echo "0")
                OVERALL_COVERED=$(grep -o 'lines-covered="[0-9]*"' "$COVERAGE_MERGED" | head -1 | grep -o '[0-9]*' | tr -d '\n' || echo "0")
                OVERALL_VALID=$(grep -o 'lines-valid="[0-9]*"' "$COVERAGE_MERGED" | head -1 | grep -o '[0-9]*' | tr -d '\n' || echo "0")
                echo "[verify] ✅ Merged coverage: ${OVERALL_COVERED}/${OVERALL_VALID} = $(awk -v r="${OVERALL_RATE:-0}" 'BEGIN {printf "%.2f", r * 100}')%"
            else
                echo "[verify] ❌ reportgenerator exited 0 but produced no Cobertura.xml — see $MERGE_LOG" >&2
                COVERAGE_MERGED=""
            fi
        else
            echo "[verify] ❌ reportgenerator failed — see $MERGE_LOG" >&2
            if [[ "$FAIL_FAST" == "1" ]]; then
                exit 1
            else
                OVERALL_RC=1
            fi
        fi
    fi
fi

# ─── Phase 7: Coverage gate ─────────────────────────────────────────────
# Per-assembly drop tolerance (1pp) + new files ≥80% line coverage.
# Only runs under --scope=full (the per-assembly "drop vs baseline"
# comparison is meaningless with partial-scope coverage numbers).
#
# Reads two inputs:
#   - .github/coverage-baseline.json (per-assembly map; may be empty
#     until the first full verify.sh run auto-populates it)
#   - TestResults/Verify/**/coverage.cobertura.xml (one per test project)
#
# The gate's Python helper parses the merged Cobertura.xml (produced
# by Phase 6.5's ReportGenerator run) to build a per-assembly + per-file
# coverage map. ReportGenerator already does the per-class line
# deduplication that a naive XML walker would miss; the script just
# extracts the assembly-level line-rate + per-file covered/valid counts.
# Files tagged `[ExcludeFromCodeCoverage]` are already excluded by
# Coverlet; the script treats missing entries as intentionally opted-out.
if [[ "$RUN_COVERAGE_GATE" == "1" ]]; then
    echo "[verify] === Phase 7: Coverage gate ==="
    BASELINE_FILE="$REPO_ROOT/.github/coverage-baseline.json"

    if [[ ! -f "$BASELINE_FILE" ]]; then
        echo "[verify] ❌ No baseline file found at $BASELINE_FILE" >&2
        set_phase_status coverage_gate failed
        if [[ "$FAIL_FAST" == "1" ]]; then
            exit 1
        else
            OVERALL_RC=1
        fi
    elif [[ -z "$COVERAGE_MERGED" || ! -f "$COVERAGE_MERGED" ]]; then
        # No merged coverage means either no tests ran (under --scope=unit etc.
        # when the test stub didn't emit cobertura XMLs, or the merge phase
        # was skipped) or the merge failed. The gate cannot evaluate, so
        # skip it — don't fail the run. The auto-raise phase handles the
        # "merge failed" case via OVERALL_RC propagation from phase 7.5.
        echo "[verify] ⚠️  No merged coverage at $COVERAGE_DIR/Cobertura.xml — gate skipped" >&2
        set_phase_status coverage_gate skipped
    else
        GATE_RESULT="$(mktemp -t verify-gate-result.XXXXXX.json)"

        # Parse the merged Cobertura.xml → per-assembly + per-file JSON,
        # then evaluate the gate rules. One Python invocation does both
        # (parse + gate eval) since they're closely coupled.
        if python3 - "$COVERAGE_MERGED" "$BASELINE_FILE" > "$GATE_RESULT" 2>&1 <<'PYEOF'
"""Parse the merged Cobertura.xml and evaluate the per-assembly + new-file gate.

Reads:
  argv[1] = merged Cobertura.xml (from ReportGenerator)
  argv[2] = baseline JSON file

Writes a structured result to stdout AND exits 0 on pass / 1 on fail.

The gate rules:
  1. Per-assembly: each assembly's coverage must not drop more than 1pp
     vs the stored baseline. New assemblies (not in baseline) are
     recorded but not gated (the auto-raise phase picks them up).
  2. New files: any .cs file in src/ added in the PR diff
     (`git diff --diff-filter=A origin/master...HEAD`) must have
     ≥80% line coverage. Files not found in the cobertura data are
     treated as intentionally opted-out (Coverlet already excludes
     files tagged [ExcludeFromCodeCoverage]).

ReportGenerator has already done the per-class line deduplication
(class-level + per-method <line> elements). We just extract the
per-assembly line-rate and the per-file covered/valid counts.
"""
import json, subprocess, sys, xml.etree.ElementTree as ET

def pct(c, v):
    return (c / v * 100.0) if v > 0 else 0.0

# Parse merged Cobertura.xml
with open(sys.argv[1]) as f:
    tree = ET.parse(f)
root = tree.getroot()

assemblies = {}
files = {}
for pkg in root.findall(".//package"):
    asm = pkg.get("name") or "?"
    if asm not in assemblies:
        assemblies[asm] = {"covered": 0, "valid": 0}
    classes_container = pkg.find("./classes")
    if classes_container is None:
        continue
    for cls in classes_container.findall("./class"):
        filename = cls.get("filename") or "?"
        if filename not in files:
            files[filename] = {"covered": 0, "valid": 0, "assembly": asm}
        # Direct <lines> child only — the per-method view is a sibling
        # (under <methods>/<method>/<lines>) and reportgenerator has
        # already merged them at the class level.
        for line in cls.findall("./lines/line"):
            files[filename]["valid"] += 1
            try:
                if int(line.get("hits", "0")) > 0:
                    files[filename]["covered"] += 1
            except ValueError:
                pass
        assemblies[asm]["covered"] += files[filename]["covered"]
        assemblies[asm]["valid"]   += files[filename]["valid"]

# Read baseline
with open(sys.argv[2]) as f:
    baseline_doc = json.load(f)
baseline = baseline_doc.get("assemblies", {})

NEW_FILE_MIN = 80.0
DROP_PP = 1.0

result = {
    "perAssembly": [],
    "newFiles": [],
    "diffFiles": [],
    "passed": True,
}

# Per-assembly drop tolerance
for asm in sorted(assemblies):
    h = assemblies[asm]
    h_pct = pct(h["covered"], h["valid"])
    base = baseline.get(asm)
    if base is None:
        result["perAssembly"].append({
            "assembly": asm, "headPct": round(h_pct, 2),
            "basePct": None, "deltaPp": None,
            "status": "new (no baseline yet)",
        })
        continue
    b_pct = pct(base["covered"], base["valid"])
    delta_pp = h_pct - b_pct
    if delta_pp < -DROP_PP:
        result["passed"] = False
        result["perAssembly"].append({
            "assembly": asm, "headPct": round(h_pct, 2),
            "basePct": round(b_pct, 2), "deltaPp": round(delta_pp, 2),
            "status": f"FAIL dropped {-delta_pp:.2f}pp",
        })
    else:
        sign = "+" if delta_pp >= 0 else ""
        result["perAssembly"].append({
            "assembly": asm, "headPct": round(h_pct, 2),
            "basePct": round(b_pct, 2), "deltaPp": round(delta_pp, 2),
            "status": "ok",
        })

# New files (gate): git diff origin/master...HEAD --diff-filter=A
# The gate enforces ≥80% line coverage on any newly-added .cs file
# in src/. Modified files don't get gated here (they're surfaced in
# the PR comment for reviewer attention but the gate is per-file-on-add
# only — a strict "no drop" gate on modified files would fight every
# refactor PR).
try:
    new_diff_out = subprocess.check_output(
        ["git", "diff", "--name-only", "--diff-filter=A", "origin/master...HEAD"],
        stderr=subprocess.DEVNULL,
    ).decode().splitlines()
except subprocess.CalledProcessError:
    new_diff_out = []

new_cs = [f for f in new_diff_out if f.endswith(".cs") and f.startswith("src/")]
for path in new_cs:
    entry = files.get(path)
    if entry is None:
        # Either [ExcludeFromCodeCoverage] (Coverlet excluded) or
        # genuinely not in cobertura (rare). Don't fail; flag it.
        result["newFiles"].append({
            "file": path, "coveragePct": None,
            "status": "unmapped (verify [ExcludeFromCodeCoverage] or not in coverage)",
        })
        continue
    cov_pct = pct(entry["covered"], entry["valid"])
    if cov_pct < NEW_FILE_MIN:
        result["passed"] = False
        result["newFiles"].append({
            "file": path, "coveragePct": round(cov_pct, 2),
            "status": f"FAIL below {NEW_FILE_MIN:.0f}%",
        })
    else:
        result["newFiles"].append({
            "file": path, "coveragePct": round(cov_pct, 2),
            "status": "ok",
        })

# All diff files (reviewer-facing): modified + new + deleted.
# The PR comment shows coverage for all files touched in this PR
# (modified + new), so reviewers can eyeball whether the changed
# code is well-tested. Not gated — just informational.
try:
    all_diff_out = subprocess.check_output(
        ["git", "diff", "--name-only", "origin/master...HEAD"],
        stderr=subprocess.DEVNULL,
    ).decode().splitlines()
except subprocess.CalledProcessError:
    all_diff_out = []

all_cs = [f for f in all_diff_out if f.endswith(".cs") and f.startswith("src/")]
for path in all_cs:
    entry = files.get(path)
    kind = "new" if path in [nf["file"] for nf in result["newFiles"]] else "modified"
    if entry is None:
        result["diffFiles"].append({
            "file": path, "kind": kind, "coveragePct": None,
            "status": "unmapped",
        })
        continue
    cov_pct = pct(entry["covered"], entry["valid"])
    result["diffFiles"].append({
        "file": path, "kind": kind, "coveragePct": round(cov_pct, 2),
        "status": "ok",
    })

# Print a per-line summary for the terminal.
print("Per-assembly gate:")
for row in result["perAssembly"]:
    if row["basePct"] is None:
        print(f"  {row['assembly']}: {row['headPct']}%  {row['status']}")
    else:
        sign = "+" if (row["deltaPp"] or 0) >= 0 else ""
        print(f"  {row['assembly']}: {row['headPct']}% (baseline {row['basePct']}%, delta {sign}{row['deltaPp']}pp)  {row['status']}")
print("New-file gate:")
if result["newFiles"]:
    for row in result["newFiles"]:
        if row["coveragePct"] is None:
            print(f"  {row['file']}: {row['status']}")
        else:
            print(f"  {row['file']}: {row['coveragePct']}%  {row['status']}")
else:
    print("  (no new .cs files in src/ in this PR)")

# Emit the structured result JSON for the auto-raise phase + comment.
print("---GATE-RESULT-BELOW---")
print(json.dumps(result, indent=2, sort_keys=True))

sys.exit(0 if result["passed"] else 1)
PYEOF
        then
            # Parse the structured result (last JSON block after the marker).
            GATE_RC=0
            sed -n '/---GATE-RESULT-BELOW---/,$p' "$GATE_RESULT" | tail -n +2 > "$RESULTS_DIR/Coverage/gate-result.json"
            echo "[verify] ✅ Coverage gate passed"
            set_phase_status coverage_gate passed
        else
            GATE_RC=1
            sed -n '/---GATE-RESULT-BELOW---/,$p' "$GATE_RESULT" | tail -n +2 > "$RESULTS_DIR/Coverage/gate-result.json"
            echo "[verify] ❌ Coverage gate failed" >&2
            set_phase_status coverage_gate failed
        fi

        # Surface the per-line summary the gate already printed.
        sed '/---GATE-RESULT-BELOW---/,$d' "$GATE_RESULT" | sed 's/^/[verify]   /'
        rm -f "$GATE_RESULT"

        if [[ "$GATE_RC" -ne 0 ]]; then
            if [[ "$FAIL_FAST" == "1" ]]; then
                exit 1
            else
                OVERALL_RC=1
            fi
        fi
    fi
fi

# ─── Phase 8: Baseline auto-raise ───────────────────────────────────────
# For each assembly where HEAD coverage > baseline coverage %, raise
# the stored baseline to the HEAD numbers. Raise-only invariant:
# never lowers the bar.
#
# Reads the merged Cobertura.xml produced by Phase 6.5's ReportGenerator
# run. ReportGenerator has already done the cross-test-project merge
# and the per-class line deduplication; this script just extracts
# per-assembly covered/valid counts.
#
# Runs regardless of pass/fail — partial runs (e.g., build failed but
# unit tests ran) can still raise the bar if their coverage data is
# valid. The dev reviews `git diff .github/coverage-baseline.json` and
# commits it themselves if they want the bar raised.
if [[ "$RUN_AUTO_RAISE" == "1" ]]; then
    echo "[verify] === Phase 8: Baseline auto-raise ==="
    BASELINE_FILE="$REPO_ROOT/.github/coverage-baseline.json"
    if [[ ! -f "$BASELINE_FILE" ]]; then
        echo "[verify] ⚠️  No baseline file at $BASELINE_FILE — skipping auto-raise"
    elif [[ -z "$COVERAGE_MERGED" || ! -f "$COVERAGE_MERGED" ]]; then
        echo "[verify] ⚠️  No merged coverage at $COVERAGE_DIR/Cobertura.xml — auto-raise skipped"
    else
        python3 - "$COVERAGE_MERGED" "$BASELINE_FILE" <<'PYEOF'
"""Parse the merged Cobertura.xml and raise per-assembly baseline numbers
where HEAD coverage is higher. Raise-only invariant: never lowers."""
import datetime
import json
import subprocess
import sys
import xml.etree.ElementTree as ET

def pct(c, v):
    return (c / v * 100.0) if v > 0 else 0.0

# Parse merged Cobertura.xml → per-assembly {covered, valid}
with open(sys.argv[1]) as f:
    tree = ET.parse(f)
root = tree.getroot()

assemblies = {}
for pkg in root.findall(".//package"):
    asm = pkg.get("name") or "?"
    if asm not in assemblies:
        assemblies[asm] = {"covered": 0, "valid": 0}
    classes_container = pkg.find("./classes")
    if classes_container is None:
        continue
    for cls in classes_container.findall("./class"):
        for line in cls.findall("./lines/line"):
            assemblies[asm]["valid"] += 1
            try:
                if int(line.get("hits", "0")) > 0:
                    assemblies[asm]["covered"] += 1
            except ValueError:
                pass

# Read baseline
with open(sys.argv[2]) as f:
    baseline_doc = json.load(f)
baseline = baseline_doc.get("assemblies", {})

raised = []
for asm in sorted(assemblies):
    h = assemblies[asm]
    base = baseline.get(asm)
    h_pct = pct(h["covered"], h["valid"])
    if base is None or h_pct > pct(base["covered"], base["valid"]):
        baseline[asm] = {"covered": h["covered"], "valid": h["valid"]}
        raised.append((asm, round(h_pct, 2)))

if raised:
    try:
        sha = subprocess.check_output(["git", "rev-parse", "--short", "HEAD"]).decode().strip()
    except subprocess.CalledProcessError:
        sha = "unknown"
    baseline_doc["assemblies"] = baseline
    baseline_doc["lastUpdated"] = datetime.date.today().isoformat()
    baseline_doc["commit"] = sha
    with open(sys.argv[2], "w") as f:
        json.dump(baseline_doc, f, indent=2, sort_keys=True)
        f.write("\n")
    print(f"[verify] Raised baseline for {len(raised)} assembly(ies):")
    for asm, p in raised:
        print(f"[verify]   {asm}: now {p}%")
    print(f"[verify] Wrote updated baseline to {sys.argv[2]}")
    print(f"[verify] Review with: git diff .github/coverage-baseline.json")
else:
    print(f"[verify] No assembly coverage improved; baseline unchanged.")
PYEOF
    fi
fi

# ─── Summary ────────────────────────────────────────────────────────────
print_final_summary "$RESULTS_DIR"

FINISHED_AT="$(date -Iseconds 2>/dev/null || date)"

# Build a newline-separated `name=status` string from PHASE_LIST for
# write_summary_json. Phases that were skipped (because their flag was
# off or scope doesn't run them) are included as "skipped" so the
# comment template can show "—" instead of omitting them.
# (PHASE_LIST was populated incrementally during the run via
# set_phase_status above.)

write_summary_json \
    "$RESULTS_DIR" \
    "$PHASE_LIST" \
    "$HEAD_SHA" \
    "$HEAD_MESSAGE" \
    "$SCOPE" \
    "$FAIL_FAST" \
    "$STARTED_AT" \
    "$FINISHED_AT" \
    "$RESULTS_DIR/Coverage/gate-result.json"

# ─── Phase 9: PR comment (opt-in via --post-comment) ─────────────────
# Off by default. When on, posts a sticky comment to the current PR
# branch (via `gh pr comment`). Success-only by default;
# --post-comment=always also posts on failure for the dev-reviewer
# handoff use case.
#
# Any error in the comment flow is non-fatal: a `gh` failure prints a
# warning but doesn't double-fail the verify script.
if [[ "$RUN_PR_COMMENT" == "1" ]]; then
    echo "[verify] === Phase 9: PR comment ==="
    COMMENT_MD="$RESULTS_DIR/comment.md"
    SUMMARY_JSON="$RESULTS_DIR/summary.json"

    # Render comment.md from summary.json
    GH_HANDLE="$(gh api user -q .login 2>/dev/null || echo local-dev)"
    PR_URL="$(gh pr view --json url -q '.url' 2>/dev/null || echo "")"
    render_comment_md "$SUMMARY_JSON" "$COMMENT_MD" "$GH_HANDLE" "$FINISHED_AT" "$PR_URL"

    # Post via gh CLI (sticky: previous comment deleted first via marker)
    post_pr_comment "$SUMMARY_JSON" "$COMMENT_MD" "$PR_COMMENT_ALWAYS"
fi

if [[ "$OVERALL_RC" == "0" ]]; then
    echo "[verify] ✅ All selected checks passed."
else
    echo "[verify] ❌ One or more checks failed." >&2
fi
exit "$OVERALL_RC"
