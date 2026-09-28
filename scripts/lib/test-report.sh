#!/usr/bin/env bash
# test-report.sh — shared helper for verify.sh.
#
# PURPOSE: Parse the TRX files that `dotnet test --logger trx …` writes to
# `TestResults/<stage>/` and emit a clean, actionable failure summary. The
# script already passes --logger trx; it just never read the output. This
# helper closes that loop so an agent running the script sees WHICH tests
# failed (and in WHICH project) without re-running with --filter.
#
# Two public functions:
#   summarize_trx <stage-name> <trx-file> <test-exit-code> [<log-prefix>]
#     Prints a per-stage block. On failure: lists each failed test name +
#     first line of the error message + a pointer to the TRX. On pass: a
#     single green ✅ line.
#
#   print_final_summary <results-dir> [<log-prefix>]
#     Walks every TRX under <results-dir> and prints the aggregate
#     pass/fail table for the run.
#
# Three stage-wrapping helpers (mirror studywise-api; tracks feature
# #683's deploy-verify gate):
#   begin_stage <stage-name>
#     Emits a `::group::<stage>` marker so GitHub Actions renders the
#     stage as a collapsible log section in the UI, and most modern
#     terminals render it as a foldable region. Legacy terminals print
#     the marker as a line of text — harmless. Pair with end_stage.
#
#   end_stage <stage-name> [<trx-file> <stage-rc>]
#     Emits `::endgroup::`. If a TRX file path is provided and the file
#     exists, also calls summarize_trx with it.
#
#   run_stage <stage-name> <stage-dir> [--no-trx] <command...>
#     All-in-one wrapper: creates <stage-dir>, emits the group marker,
#     runs the command with stdout/stderr tee'd to <stage-dir>/console.log
#     and <stage-dir>/stderr.log, emits the end marker, and (if the
#     command produced a *.trx in <stage-dir>) prints the TRX failure
#     summary. Pass --no-trx for stages that don't produce a TRX
#     (formatting check, security scan, coverage gate, pre-flights).
#     Returns the wrapped command's exit code.
#
# The tee'd logs give local devs (and CI) a grep-able per-stage failure
# trail independent of the TRX summary.
#
# Parsing chain (cross-platform — macOS + WSL, no xmllint dep):
#   1. POSIX awk (BSD awk on macOS, gawk/mawk on WSL are identical for the
#      extraction we do). TRX attributes are single-line, double-quoted,
#      no XML edge cases hit.
#   2. python3 + xml.etree.ElementTree (stdlib, present in pyenv shim and
#      in WSL distros).
#   3. Graceful no-op: if both fail or the file is missing, the helper
#      prints a clear pointer and returns 1 so the caller knows the
#      summary was incomplete, without breaking the run.
#
# Usage (from verify.sh):
#   LOG_PREFIX="[verify]"
#   source "$(dirname "${BASH_SOURCE[0]}")/lib/test-report.sh"
#
#   summarize_trx "Integration tests" "$trx_file" "$test_rc"
#   print_final_summary "$RESULTS_DIR"

# ─── Internal: parse a TRX file into shell vars + array ────────────────
# Sets globals on success: PARSED_TOTAL, PARSED_PASSED, PARSED_FAILED,
# PARSED_FAILED_TESTS=("name|message" …). Returns 0 on parse (even if
# nothing failed), 1 on parse failure.
parse_trx() {
    local trx_file="$1"
    PARSED_TOTAL=0
    PARSED_PASSED=0
    PARSED_FAILED=0
    PARSED_FAILED_TESTS=()

    if [[ ! -f "$trx_file" ]] || [[ ! -r "$trx_file" ]]; then
        return 1
    fi

    local raw=""
    raw="$(_parse_trx_awk "$trx_file")"
    if [[ -z "$raw" ]] || [[ "$raw" == "PARSE_FAIL" ]]; then
        if command -v python3 >/dev/null 2>&1; then
            raw="$(_parse_trx_python "$trx_file")" || raw="PARSE_FAIL"
        fi
    fi
    if [[ -z "$raw" ]] || [[ "$raw" == "PARSE_FAIL" ]]; then
        return 1
    fi

    while IFS= read -r line; do
        case "$line" in
            COUNTS:*)
                local rest="${line#COUNTS:}"
                PARSED_TOTAL="${rest%%:*}"
                rest="${rest#*:}"
                PARSED_PASSED="${rest%%:*}"
                PARSED_FAILED="${rest#*:}"
                : "${PARSED_TOTAL:=0}" "${PARSED_PASSED:=0}" "${PARSED_FAILED:=0}"
                PARSED_TOTAL=$((10#$PARSED_TOTAL))
                PARSED_PASSED=$((10#$PARSED_PASSED))
                PARSED_FAILED=$((10#$PARSED_FAILED))
                ;;
            FAILED_TEST:*)
                local entry="${line#FAILED_TEST:}"
                local name="${entry%%:*}"
                local msg="${entry#*:}"
                PARSED_FAILED_TESTS+=("${name}|${msg}")
                ;;
        esac
    done <<<"$raw"
}

# ─── Internal: awk primary parser ──────────────────────────────────────
# Two gotchas worth knowing:
#   1. RSTART/RLENGTH are global and sticky. extract_num/extract_str call
#      match() and overwrite those vars, so any rule whose body relies on
#      RSTART/RLENGTH after a call to those functions must re-match
#      explicitly with match() — implicit pattern matches in `rule {}`
#      do NOT reset RSTART/RLENGTH.
#   2. <Message>…</Message> can appear on the same line OR split across
#      multiple lines (NUnit/xunit emits single-line for short asserts,
#      multi-line for assertions that include Expected/Actual blocks).
#      Handle both.
_parse_trx_awk() {
    local trx_file="$1"
    awk '
    function extract_num(attr,   s) {
        if (match($0, attr "=\"")) {
            s = substr($0, RSTART + RLENGTH)
            sub("\".*", "", s)
            return s + 0
        }
        return 0
    }
    function extract_str(attr,   s) {
        if (match($0, attr "=\"")) {
            s = substr($0, RSTART + RLENGTH)
            sub("\".*", "", s)
            return s
        }
        return ""
    }
    /<Counters [^>]*passed=/ {
        total  = extract_num("total")
        passed = extract_num("passed")
        failed = extract_num("failed")
    }
    /<UnitTestResult / {
        if (extract_str("outcome") == "Failed") {
            current_failed = extract_str("testName")
        }
    }
    {
        if (match($0, /<Message>/) && current_failed != "") {
            after_open = substr($0, RSTART + RLENGTH)
            if (match(after_open, /<\/Message>/)) {
                # Single-line case: <Message>foo</Message> on the same line.
                content = substr(after_open, 1, RSTART - 1)
                sub(/^[[:space:]]+/, "", content)
                sub(/[[:space:]]+$/, "", content)
                if (content != "") {
                    if (length(content) > 200) content = substr(content, 1, 197) "..."
                    print "FAILED_TEST:" current_failed ":" content
                }
                current_failed = ""
            } else {
                # Multi-line case: <Message>foo\nbar\n...\n</Message>.
                # Print whatever follows the open tag on the same line
                # (xunit Assert.Equal puts the assertion text there
                # and pushes the object dump to subsequent lines), then
                # let the per-line rules continue accumulating until we
                # see </Message>. We stop after the FIRST line of the dump
                # so we do not drown the summary in object trees -- the TRX
                # is the source of full detail.
                first_line = after_open
                sub(/^[[:space:]]+/, "", first_line)
                sub(/[[:space:]]+$/, "", first_line)
                if (first_line != "") {
                    if (length(first_line) > 200) first_line = substr(first_line, 1, 197) "..."
                    print "FAILED_TEST:" current_failed ":" first_line
                }
                in_msg = 1
            }
            next
        }
    }
    /<\/Message>/ && in_msg { in_msg = 0; current_failed = ""; next }
    # Subsequent lines of a multi-line <Message> dump are consumed (skipped).
    # The first line was already printed by the multi-line branch above.
    # Continuation lines (object-tree dumps from xunit Assert.Equal) are
    # not printed in the summary -- the TRX file is the source of full detail.
    END { print "COUNTS:" total ":" passed ":" failed }
    ' "$trx_file" 2>/dev/null || echo "PARSE_FAIL"
}

# ─── Internal: python3 fallback ────────────────────────────────────────
_parse_trx_python() {
    local trx_file="$1"
    python3 - "$trx_file" <<'PYEOF' 2>/dev/null || echo "PARSE_FAIL"
import sys
import xml.etree.ElementTree as ET

trx_file = sys.argv[1]
NS = {"trx": "http://microsoft.com/schemas/VisualStudio/TeamTest/2010"}

try:
    tree = ET.parse(trx_file)
except Exception:
    sys.exit(1)

root = tree.getroot()
counters = root.find(".//trx:Counters", NS)
if counters is not None:
    total  = counters.get("total", "0") or "0"
    passed = counters.get("passed", "0") or "0"
    failed = counters.get("failed", "0") or "0"
    print(f"COUNTS:{total}:{passed}:{failed}")
else:
    print("COUNTS:0:0:0")

for result in root.findall(".//trx:UnitTestResult", NS):
    if result.get("outcome") == "Failed":
        test_name = result.get("testName") or "?"
        msg_elem = result.find(".//trx:Message", NS)
        msg = ""
        if msg_elem is not None and msg_elem.text:
            lines = [ln for ln in msg_elem.text.strip().splitlines() if ln.strip()]
            msg = lines[0] if lines else "(no message)"
        else:
            msg = "(no message)"
        if len(msg) > 200:
            msg = msg[:197] + "..."
        print(f"FAILED_TEST:{test_name}:{msg}")
PYEOF
}

# ─── Public: per-stage block ───────────────────────────────────────────
summarize_trx() {
    local stage="$1"
    local trx_file="$2"
    local test_rc="$3"
    local prefix="${4:-${LOG_PREFIX:-ci}}"

    if [[ ! -f "$trx_file" ]]; then
        if [[ "$test_rc" -ne 0 ]]; then
            echo "[${prefix}] ❌ $stage: tests failed (no TRX file at $trx_file)" >&2
        fi
        return 1
    fi

    if ! parse_trx "$trx_file"; then
        echo "[${prefix}] ⚠️  $stage: could not parse $trx_file — see file directly for details" >&2
        return 1
    fi

    if [[ "$PARSED_FAILED" -gt 0 ]] || [[ "$test_rc" -ne 0 ]]; then
        echo "[${prefix}] ❌ $stage: ${PARSED_PASSED}/${PARSED_TOTAL} passed, ${PARSED_FAILED} failed"
        local entry name msg msg_one_line
        for entry in "${PARSED_FAILED_TESTS[@]:-}"; do
            [[ -z "$entry" ]] && continue
            name="${entry%%|*}"
            msg="${entry#*|}"
            # Collapse the message to a single line and trim trailing
            # whitespace so truncated xunit object-tree dumps don't
            # print a half-line like "      {" before the truncation
            # marker. Keep the first 200 chars; TRX is the source of full
            # detail.
            msg_one_line="${msg//$'\n'/ }"
            msg_one_line="${msg_one_line#"${msg_one_line%%[![:space:]]*}"}"
            msg_one_line="${msg_one_line%"${msg_one_line##*[![:space:]]}"}"
            echo "  - ${name}: ${msg_one_line}"
        done
        echo "    Full results: $trx_file"
    else
        echo "[${prefix}] ✅ $stage: ${PARSED_PASSED}/${PARSED_TOTAL} passed"
    fi
}

# ─── Public: aggregate table at end of run ─────────────────────────────
print_final_summary() {
    local results_dir="$1"
    local prefix="${2:-${LOG_PREFIX:-ci}}"

    echo
    echo "[${prefix}] Summary:"

    local found_any=0
    local total_all=0 passed_all=0 failed_all=0
    local trx stage label total passed failed label_clean
    while IFS= read -r -d '' trx; do
        stage="$(basename "$trx" .trx)"
        if ! parse_trx "$trx"; then
            echo "  $stage: (could not parse $trx)"
            continue
        fi
        if [[ "$PARSED_TOTAL" -eq 0 && "$PARSED_FAILED" -eq 0 ]]; then
            continue
        fi
        total=$PARSED_TOTAL
        passed=$PARSED_PASSED
        failed=$PARSED_FAILED
        label="${passed}/${total} passed"
        if [[ "$failed" -gt 0 ]]; then
            label="${label}, ${failed} failed"
        fi
        # CLI test project files use "Studywise.CLI.<Stage>" naming —
        # strip the prefix for compactness.
        label_clean="${stage%.fast}"
        label_clean="${label_clean#Studywise.CLI.}"
        label_clean="${label_clean#Studywise.Tests.}"
        echo "  ${label_clean}: ${label}"
        total_all=$((total_all + total))
        passed_all=$((passed_all + passed))
        failed_all=$((failed_all + failed))
        found_any=1
    done < <(find "$results_dir" -maxdepth 2 -name "*.trx" -print0 2>/dev/null | sort -z)

    if [[ "$found_any" -eq 0 ]]; then
        echo "  (no TRX files found under $results_dir)"
        return 0
    fi

    if [[ "$failed_all" -gt 0 ]]; then
        echo "[${prefix}] Totals: ${passed_all}/${total_all} passed, ${failed_all} failed across stages."
    fi
}

# ─── Public: stage wrapping (GitHub Actions log groups + tee'd logs) ────
# Mirror studywise-api's test-report.sh: resolves the "tee per-stage"
# TODO that lets GitHub Actions render the script's output as
# collapsible log sections.

begin_stage() {
    local stage="$1"
    local prefix="${LOG_PREFIX:-ci}"
    # ::group:: is a no-op outside GitHub Actions; modern terminals that
    # recognise it (e.g. iTerm2, Windows Terminal, recent gnome-terminal)
    # render the wrapped output as a foldable region.
    echo "::group::${stage}"
    echo "[${prefix}] === ${stage} ==="
}

end_stage() {
    local stage="$1"
    local trx_file="${2:-}"
    local stage_rc="${3:-0}"
    local prefix="${LOG_PREFIX:-ci}"
    echo "::endgroup::"
    if [[ -n "$trx_file" ]] && [[ -f "$trx_file" ]]; then
        summarize_trx "$stage" "$trx_file" "$stage_rc" "$prefix"
    fi
}

# Wrap a command with begin/end markers, tee stdout to <stage>/console.log,
# tee stderr to <stage>/stderr.log, and (if a *.trx file appears in the
# stage dir) print the TRX failure summary.
#
# Usage:
#   run_stage "Unit tests" "$RESULTS_DIR/Unit" \
#       dotnet test "$TEST_PROJ" "${DOTNET_TEST_FLAGS[@]}" \
#           --logger "trx;LogFileName=unit.trx"
#   rc=$?   # command's exit code
#
# Side effect: writes the discovered TRX (if any) path to the global
# LAST_TRX_FILE so other helpers (write_summary_json) can pick it up
# without re-discovering the file.
#
# For stages that don't produce a TRX (format, security scan, coverage
# gate), pass --no-trx and no summary is printed:
#   run_stage "Code formatting" "$RESULTS_DIR/Format" --no-trx \
#       dotnet format "$SLN" --verify-no-changes --verbosity minimal
run_stage() {
    local stage="$1"
    local stage_dir="$2"
    shift 2

    local expect_trx=1
    if [[ "${1:-}" == "--no-trx" ]]; then
        expect_trx=0
        shift
    fi

    mkdir -p "$stage_dir"
    local console_log="$stage_dir/console.log"
    local stderr_log="$stage_dir/stderr.log"

    begin_stage "$stage"

    # stdout → console.log (tee shows it on terminal too).
    # stderr → stderr.log via process substitution so the terminal still
    # sees it interleaved. PIPESTATUS[0] is the wrapped command's rc.
    "$@" 2> >(tee "$stderr_log" >&2) | tee "$console_log"
    local rc=${PIPESTATUS[0]}

    local trx_file=""
    LAST_TRX_FILE=""
    if [[ "$expect_trx" -eq 1 ]]; then
        # Find the first *.trx in the stage dir. dotnet test writes it
        # as <LogFileName> in --results-directory; we pass a stable name
        # from the caller so there's at most one.
        trx_file="$(find "$stage_dir" -maxdepth 1 -name '*.trx' -print -quit 2>/dev/null || true)"
        LAST_TRX_FILE="$trx_file"
    fi

    end_stage "$stage" "$trx_file" "$rc"
    return "$rc"
}

# ─── Public: extract pass/fail/skip counts from a TRX file ─────────────
# Sets globals: TRX_TOTAL, TRX_PASSED, TRX_FAILED, TRX_SKIPPED.
# Returns 0 on success (even when counts are zero), 1 if the TRX is
# missing or unparseable.
trx_counts() {
    local trx_file="$1"
    TRX_TOTAL=0
    TRX_PASSED=0
    TRX_FAILED=0
    TRX_SKIPPED=0

    if [[ ! -f "$trx_file" ]]; then
        return 1
    fi

    parse_trx "$trx_file" || return 1
    TRX_TOTAL="$PARSED_TOTAL"
    TRX_PASSED="$PARSED_PASSED"
    TRX_FAILED="$PARSED_FAILED"

    # Skip counts aren't surfaced by parse_trx's aggregate counters.
    # Pull them from the UnitTestResult elements directly.
    local raw=""
    raw="$(_parse_trx_awk "$trx_file")"
    if [[ -z "$raw" || "$raw" == "PARSE_FAIL" ]]; then
        if command -v python3 >/dev/null 2>&1; then
            raw="$(_parse_trx_python "$trx_file")" || raw="PARSE_FAIL"
        fi
    fi
    TRX_SKIPPED=$(printf '%s\n' "$raw" | grep -c '^SKIPPED_TEST:' || true)
    return 0
}

# ─── Public: write a structured summary.json ───────────────────────────
# Aggregates per-stage pass/fail status + TRX counts + the coverage
# gate result into TestResults/<scope>/summary.json. Designed to feed
# the PR comment helper and any future programmatic consumers.
#
# Usage:
#   PHASES=(preflight=passed restore=passed build=failed tests=passed)
#   write_summary_json "$RESULTS_DIR" "$(printf '%s\n' "${PHASES[@]}")"
#
# Each phase entry is `name=status` (status ∈ passed|failed|skipped).
# The function reads TestResults/<scope>/<phase>/*.trx for tests and
# TestResults/<scope>/Coverage/gate-result.json for coverage.
write_summary_json() {
    local results_dir="$1"
    local phase_list="$2"   # newline-separated name=status lines
    local head_sha="$3"
    local head_message="$4"
    local scope="$5"
    local fail_fast="$6"
    local started_at="$7"
    local finished_at="$8"
    local coverage_gate_file="$9"

    python3 - "$results_dir" "$phase_list" "$head_sha" "$head_message" \
            "$scope" "$fail_fast" "$started_at" "$finished_at" \
            "$coverage_gate_file" \
        "$(dirname "${BASH_SOURCE[0]}")" <<'PYEOF'
"""Build TestResults/Verify/summary.json from per-stage state."""
import datetime as dt
import glob
import json
import os
import subprocess
import sys

(results_dir, phase_list, head_sha, head_message,
 scope, fail_fast, started_at, finished_at,
 coverage_gate_file, lib_dir) = sys.argv[1:11]

def parse_phase(line):
    line = line.strip()
    if not line or "=" not in line:
        return None
    name, _, status = line.partition("=")
    return name.strip(), status.strip()

phases = {}
for line in phase_list.splitlines():
    parsed = parse_phase(line)
    if parsed:
        phases[parsed[0]] = parsed[1]

# Test counts from each TRX under <results_dir>/<Stage>/*.trx.
# CLI has 3 test projects (Unit / Integration / E2E); api has 4.
# Anything else that lands in <results_dir>/<OtherStage>/*.trx is
# picked up opportunistically — keeps the script forward-compatible
# with future test project additions.
test_stages = ["Unit", "Integration", "E2E"]
extra_stages = sorted(
    d for d in glob.glob(os.path.join(results_dir, "*"))
    if os.path.isdir(d) and os.path.basename(d) not in test_stages
       and glob.glob(os.path.join(d, "*.trx"))
)
for d in extra_stages:
    test_stages.append(os.path.basename(d))

tests = {}
for stage in test_stages:
    trx_files = sorted(glob.glob(os.path.join(results_dir, stage, "*.trx")))
    if not trx_files:
        continue
    # Take the latest TRX (in case of retry)
    trx = trx_files[-1]
    try:
        import xml.etree.ElementTree as ET
        NS = {"trx": "http://microsoft.com/schemas/VisualStudio/TeamTest/2010"}
        tree = ET.parse(trx)
        root = tree.getroot()
        counters = root.find(".//trx:Counters", NS)
        if counters is None:
            continue
        def n(attr):
            v = counters.get(attr, "0")
            return int(v) if v.isdigit() else 0
        total = n("total")
        passed = n("passed")
        failed = n("failed")
        # skipped = total - passed - failed (covers NotRunnable + Inconclusive)
        skipped = max(0, total - passed - failed)
        tests[stage.lower()] = {
            "total": total,
            "passed": passed,
            "failed": failed,
            "skipped": skipped,
        }
    except Exception:
        continue

# Coverage gate result
coverage_data = {}
if coverage_gate_file and os.path.isfile(coverage_gate_file):
    try:
        with open(coverage_gate_file) as f:
            coverage_data = json.load(f)
    except Exception:
        pass

# Duration
duration_sec = None
try:
    fmt = "%Y-%m-%dT%H:%M:%S%z"
    s = dt.datetime.fromisoformat(started_at.replace("Z", "+00:00"))
    e = dt.datetime.fromisoformat(finished_at.replace("Z", "+00:00"))
    duration_sec = int((e - s).total_seconds())
except Exception:
    duration_sec = None

result = {
    "headSha": head_sha,
    "headMessage": head_message,
    "scope": scope,
    "failFast": fail_fast == "1",
    "startedAt": started_at,
    "finishedAt": finished_at,
    "durationSec": duration_sec,
    "phases": phases,
    "tests": tests,
    "coverage": coverage_data,
}

out_path = os.path.join(results_dir, "summary.json")
with open(out_path, "w") as f:
    json.dump(result, f, indent=2, sort_keys=True)
    f.write("\n")
print(f"[verify] Wrote {out_path}")
PYEOF
}

# ─── Public: render comment.md from summary.json ───────────────────────
# Produces a Markdown body suitable for `gh pr comment --body-file`.
# Includes: per-stage pass/fail table, files-in-diff coverage table
# (success case only), gate verdicts, head SHA, duration, attribution.
#
# Usage:
#   render_comment_md "$RESULTS_DIR/summary.json" \
#       "$RESULTS_DIR/comment.md" "@<gh-handle>" "<iso-timestamp>"
#
# On gate failure, the coverage section is collapsed to a one-line
# verdict per failed gate (no file-level enumeration — the dev re-runs
# `verify.sh` locally for the breakdown).
render_comment_md() {
    local summary_json="$1"
    local comment_md="$2"
    local gh_handle="$3"
    local iso_timestamp="$4"
    local pr_url="$5"  # optional — if empty, omit the link

    if [[ ! -f "$summary_json" ]]; then
        echo "[verify] ❌ render_comment_md: $summary_json not found" >&2
        return 1
    fi

    python3 - "$summary_json" "$comment_md" "$gh_handle" "$iso_timestamp" "$pr_url" <<'PYEOF'
"""Render a PR comment body (Markdown) from summary.json.

Structured summary post:
  - One-line status header
  - Run metadata (commit, duration, scope)
  - Per-stage check results (compact table)
  - Files in this PR (reviewer-facing coverage table)
  - Gate verdicts (one line per gate)
  - Footer

Compact enough to scan in a conversation thread, detailed enough
to be useful. No per-assembly breakdown — that's gate implementation
detail, not reviewer info.
"""
import json
import sys

(summary_json, comment_md, gh_handle, iso_timestamp, pr_url) = sys.argv[1:6]

with open(summary_json) as f:
    summary = json.load(f)

phases = summary.get("phases", {})
tests = summary.get("tests", {})
coverage = summary.get("coverage", {})
duration_sec = summary.get("durationSec")
scope = summary.get("scope", "?")
fail_fast = summary.get("failFast", True)
head_sha = (summary.get("headSha") or "?").strip()
head_message = (summary.get("headMessage") or "?").strip()
short_sha = head_sha[:7] if len(head_sha) >= 7 else head_sha

def fmt_duration(s):
    if s is None:
        return "?"
    if s < 60:
        return f"{s}s"
    m, sec = divmod(s, 60)
    return f"{m}m {sec}s"

# "skipped" doesn't count as failure — only "failed" does.
overall_ok = all(v != "failed" for v in phases.values())
has_failures = any(v == "failed" for v in phases.values())
icon = "✅" if overall_ok else "❌"
word = "Verify passed" if overall_ok else "Verify failed"

lines = []
lines.append(f"## {icon} {word} — `{short_sha}`")
lines.append("")
lines.append(f"*{head_message}*")
if pr_url:
    lines.append(f"[PR #{short_sha} →]({pr_url})")
lines.append(f"Duration: {fmt_duration(duration_sec)} · Scope: `{scope}` ({'fail-fast' if fail_fast else 'collect-all'})")
lines.append("")

# Per-stage check results
lines.append("### Checks")
lines.append("")
lines.append("| Stage | Result |")
lines.append("|---|---|")
for phase_name, status in phases.items():
    s_icon = "✅" if status == "passed" else ("❌" if status == "failed" else "⚠️")
    label = phase_name.replace("_", " ").title()
    if phase_name in tests and tests[phase_name]:
        t = tests[phase_name]
        detail = f"{t['passed']}/{t['total']} passed"
        if t["failed"]:
            detail += f", {t['failed']} failed"
        if t["skipped"]:
            detail += f", {t['skipped']} skipped"
        lines.append(f"| {label} | {s_icon} {detail} |")
    else:
        lines.append(f"| {label} | {s_icon} {status} |")
lines.append("")

# Coverage section: only files-in-diff (reviewer-facing) + gate verdicts.
has_coverage_data = (
    coverage.get("diffFiles")
    or coverage.get("perAssembly")
    or coverage.get("newFiles")
)
if has_coverage_data:
    lines.append("### Coverage")
    lines.append("")
    if coverage.get("diffFiles"):
        lines.append("**Files in this PR:**")
        lines.append("")
        lines.append("| File | Kind | Coverage |")
        lines.append("|---|---|---|")
        for row in coverage["diffFiles"]:
            cov = "⚠️ unmapped" if row.get("coveragePct") is None else f"{row['coveragePct']}%"
            lines.append(f"| `{row['file']}` | {row.get('kind', 'modified')} | {cov} |")
        lines.append("")

    # Gate verdicts — one line per gate.
    per_asm_failed = (
        coverage.get("perAssembly") is not None
        and any(not r["status"].startswith(("ok", "new"))
                for r in coverage.get("perAssembly", []))
    )
    new_file_failed = (
        coverage.get("newFiles") is not None
        and any(r.get("status", "").startswith("FAIL")
                for r in coverage.get("newFiles", []))
    )
    gate_lines = []
    if coverage.get("perAssembly") is not None:
        gate_lines.append(f"- {'❌ Per-assembly gate' if per_asm_failed else '✅ Per-assembly gate'}")
    if coverage.get("newFiles") is not None:
        gate_lines.append(f"- {'❌ New-files gate' if new_file_failed else '✅ New-files gate'}")
    if gate_lines:
        lines.append("**Gates:**")
        lines.append("")
        lines.extend(gate_lines)
        lines.append("")
else:
    # No coverage at all (e.g., --scope=unit without --post-comment-merged)
    lines.append("### Coverage")
    lines.append("")
    lines.append(f"_Coverage gate skipped under `--scope={scope}`._")
    lines.append("")

# Footer
lines.append("---")
lines.append("")
lines.append(f"_Generated by `verify.sh --post-comment`{'=always' if has_failures else ''}_")
lines.append("")

with open(comment_md, "w") as f:
    f.write("\n".join(lines))
print(f"[verify] Wrote {comment_md}")
PYEOF
}

# ─── Public: post a sticky PR comment via gh CLI ───────────────────────
# Detects the PR number from the current branch (via `gh pr view`),
# renders comment.md from summary.json, and posts via
# `gh pr comment --body-file`.
#
# If --post-comment=always isn't set (default), this is a no-op on a
# failed run (the dev re-runs locally for the breakdown).
#
# Fails soft: any error prints a warning but does not propagate, so a
# comment-posting failure doesn't double-fail the verify script.
post_pr_comment() {
    local summary_json="$1"
    local comment_md="$2"
    local always="${3:-0}"   # 1 = post on success OR failure; 0 = success-only

    if ! command -v gh >/dev/null 2>&1; then
        echo "[verify] ⚠️  gh CLI not found; skipping --post-comment" >&2
        return 0
    fi

    if ! gh auth status >/dev/null 2>&1; then
        echo "[verify] ⚠️  gh not authenticated; skipping --post-comment. Run: gh auth login" >&2
        return 0
    fi

    # Detect PR number from current branch.
    local pr_number=""
    pr_number="$(gh pr view --json number -q '.number' 2>/dev/null || true)"
    if [[ -z "$pr_number" ]]; then
        echo "[verify] ⚠️  --post-comment: not on a PR branch (or PR has no number); skipping" >&2
        echo "    Make sure your branch is pushed and has an open PR, then re-run." >&2
        return 0
    fi

    # Determine overall pass/fail from summary.json. Only "failed"
    # phases count — "skipped" (phases that didn't run because of
    # --scope or --skip-* flags) doesn't mean the run failed.
    local overall_ok=1
    if command -v jq >/dev/null 2>&1; then
        local failed
        failed=$(jq -r '[.phases // {} | to_entries[] | select(.value == "failed") | .key] | length' "$summary_json" 2>/dev/null || echo "0")
        if [[ "$failed" != "0" ]]; then overall_ok=0; fi
    else
        # Fallback: assume ok and let `gh` fail loudly if not.
        overall_ok=1
    fi

    if [[ "$overall_ok" != "1" && "$always" != "1" ]]; then
        echo "[verify] ⚠️  --post-comment: skipping (run failed; re-run with --post-comment=always to post a failure trace)"
        return 0
    fi

    if [[ ! -f "$comment_md" ]]; then
        echo "[verify] ❌ --post-comment: $comment_md not found" >&2
        return 1
    fi

    # Sticky: delete the previous comment from this user on this PR
    # before posting the new one. gh CLI has no way to identify "the
    # last comment by THIS user" specifically — `--delete-last` deletes
    # the user's most recent comment regardless of which PR it's on,
    # so we restrict by passing the PR number.
    #
    # We track the comment ID locally anyway so we can verify the
    # sticky logic worked; the marker file is the source of truth for
    # which comment ID belongs to which PR.
    local marker_dir="${XDG_CACHE_HOME:-$HOME/.cache}/studywise-verify-comments"
    mkdir -p "$marker_dir"
    local marker="$marker_dir/$pr_number.comment-id"
    if [[ -f "$marker" ]]; then
        gh pr comment "$pr_number" --delete-last --yes >/dev/null 2>&1 || true
    fi

    # Post the new comment and capture its ID for next time.
    local post_out
    post_out="$(gh pr comment "$pr_number" --body-file "$comment_md" 2>&1)" || {
        echo "[verify] ⚠️  gh pr comment failed: $post_out" >&2
        return 0
    }

    # Try to capture the comment ID. `gh pr comment` returns a URL
    # like `https://github.com/.../pull/N#issuecomment-12345`. Extract
    # the numeric part after 'issuecomment-'.
    local new_id
    new_id="$(printf '%s\n' "$post_out" | grep -oE 'issuecomment-[0-9]+' | sed 's/issuecomment-//' | head -1 || true)"
    if [[ -n "$new_id" ]]; then
        echo "$new_id" > "$marker"
    fi

    echo "[verify] ✅ Posted sticky PR comment to PR #$pr_number"
    return 0
}