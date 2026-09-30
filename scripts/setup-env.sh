#!/usr/bin/env bash
# scripts/setup-env.sh — friendly one-shot bootstrap for the local
# development environment of the Studywise CLI.
#
# PURPOSE: a single entry point that prepares everything needed to run
# the Studywise CLI tests on a fresh developer machine. Each tool can
# be installed individually, or all of them at once.
#
# Usage:
#   ./scripts/setup-env.sh                  # install everything missing
#   ./scripts/setup-env.sh gitleaks         # install just gitleaks
#   ./scripts/setup-env.sh actionlint       # install just actionlint
#   ./scripts/setup-env.sh reportgenerator  # install just reportgenerator
#   ./scripts/setup-env.sh node             # install Node.js
#   ./scripts/setup-env.sh --check          # exit 0 if all ready, else list missing
#   ./scripts/setup-env.sh --check gitleaks # exit 0 if gitleaks ready, else list missing
#   ./scripts/setup-env.sh --help
#
# Exit codes:
#   0   all requested tools ready (or successfully installed)
#   1   invalid arguments
#   2   npm not installed (Node handler; auto-installs Node first)
#   5   --check mode: one or more requested tools missing (message lists which)
#   6   install failed (Node / gitleaks / actionlint / reportgenerator)
#
# Adding a new tool:
#   1. Write a function `setup_<name>` that exits 0 on success / ready,
#      non-zero with a clear error message on failure.
#   2. Add `<name>` to the TOOLS array below.
#   3. Add a `--check` block that probes readiness (binary on PATH,
#      env var set, port reachable — whichever applies).
#   4. Update docs/devenv/setup.md.

set -uo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"

# ─── Tool registry ─────────────────────────────────────────────────────
# Order matters: prerequisites first (node, before reportgenerator/gitleaks/
# actionlint's npm-based install path). CLI has 4 installable tools;
# STUDYWISE_API_KEY is handled by verify.sh's preflight_api_key directly
# (env var, not a tool install).
TOOLS=(node reportgenerator gitleaks actionlint)

usage() {
    cat <<EOF
Usage: $0 [--check] [TOOL...]

Bootstraps the Studywise CLI local development environment. With no
arguments, runs every registered tool. With one or more TOOL names,
runs only those.

Tools: $(printf '%s ' "${TOOLS[@]}")

Note: cspell is invoked via \`npx cspell@10\` on demand from verify.sh
(Phase 5c); it has no separate setup step. The Node prerequisite is
already covered by the \`node\` entry above.

STUDYWISE_API_KEY is not needed for local test runs. Integration
tests build a WireMock server in-process and never dial the real
Studywise API. See docs/testing/testing-strategy.md.

Flags:
  --check   Don't install anything; just verify what's already ready.
            Exits 0 if all requested tools are ready, 5 with a
            per-tool pointer otherwise. Used by verify.sh
            pre-flights.
  --help    Show this message.

Examples:
  $0                       # install everything
  $0 gitleaks actionlint   # install just the defensive layers
  $0 --check               # verify everything is ready
  $0 --check reportgenerator
EOF
}

# ─── Argument parsing ──────────────────────────────────────────────────
CHECK_MODE=0
REQUESTED=()

while [[ $# -gt 0 ]]; do
    case "$1" in
        --check)
            CHECK_MODE=1
            shift
            ;;
        --help|-h)
            usage
            exit 0
            ;;
        -*)
            echo "setup-env.sh: unknown flag: $1" >&2
            usage >&2
            exit 1
            ;;
        *)
            REQUESTED+=("$1")
            shift
            ;;
    esac
done

# Default to every registered tool when no explicit tool was given
# (e.g. just `--check`, or no args).
if [[ ${#REQUESTED[@]} -eq 0 ]]; then
    REQUESTED=("${TOOLS[@]}")
fi

# Validate every requested tool name against the registry.
for tool in "${REQUESTED[@]:-${TOOLS[@]}}"; do
    case " ${TOOLS[*]} " in
        *" $tool "*) ;;
        *)
            echo "setup-env.sh: unknown tool: $tool (known: ${TOOLS[*]})" >&2
            exit 1
            ;;
    esac
done

# ─── Platform detection ────────────────────────────────────────────────
is_wsl() {
    [[ -r /proc/version ]] && grep -qi 'microsoft\|wsl' /proc/version 2>/dev/null
}

is_macos() {
    [[ "$(uname -s 2>/dev/null)" == "Darwin" ]]
}

is_linux_native() {
    [[ "$(uname -s 2>/dev/null)" == "Linux" ]] && ! is_wsl
}

# ─── Tool: node ────────────────────────────────────────────────────────
# Node is a hard prerequisite for the Azurite install (npm install -g
# azurite). On WSL in particular, both `node` and `azurite` are commonly
# installed only on the Windows side via npm, leaving the Linux shell
# with PATH entries that resolve to Windows shims which can't actually
# exec — same trap we fixed for Azurite. The runtime probe below
# catches both.
#
# Resolution order: `command -v node` → runnable probe → install.
node_check() {
    local bin
    if bin=$(command -v node 2>/dev/null); then
        if node_runnable "$bin"; then
            local ver
            ver=$("$bin" --version 2>/dev/null || echo unknown)
            echo "ready (PATH=$bin, version=$ver)"
            return 0
        fi
        echo "unrunnable ($bin is on PATH but failed --version probe — likely a WSL shim without a working node)"
        return 1
    fi
    echo "missing"
    return 1
}

node_runnable() {
    local bin="$1"
    timeout 5 "$bin" --version >/dev/null 2>&1
}

node_install() {
    echo "─── Node.js ───"

    if command -v node >/dev/null 2>&1 && node_runnable "$(command -v node)"; then
        echo "Node already runnable: $(command -v node) ($(node --version 2>/dev/null || echo unknown))"
        echo "Skipping install."
        return 0
    fi

    if is_macos; then
        echo "Detected macOS. Installing Node via Homebrew:"
        echo "  brew install node"
        if ! command -v brew >/dev/null 2>&1; then
            echo "setup-env.sh: brew not found on PATH." >&2
            echo "    Install Homebrew first: https://brew.sh" >&2
            echo "    Or install Node manually: https://nodejs.org/en/download" >&2
            return 6
        fi
        if ! brew install node; then
            echo "setup-env.sh: brew install node failed." >&2
            return 6
        fi
        return 0
    fi

    if is_linux_native || is_wsl; then
        # Detect distro family.
        if [[ -r /etc/os-release ]]; then
            . /etc/os-release
            case "${ID:-}" in
                ubuntu|debian|pop|linuxmint|elementary|kali|raspbian)
                    echo "Detected Debian/Ubuntu family (${ID}). Installing Node via apt:"
                    echo "  sudo apt-get update && sudo apt-get install -y nodejs npm"
                    if ! command -v sudo >/dev/null 2>&1 && [[ "$EUID" -ne 0 ]]; then
                        echo "setup-env.sh: apt-get install requires root. Re-run with sudo, or run:" >&2
                        echo "    sudo apt-get update && sudo apt-get install -y nodejs npm" >&2
                        return 6
                    fi
                    local sudo_cmd=""
                    [[ "$EUID" -ne 0 ]] && sudo_cmd="sudo"
                    if ! $sudo_cmd apt-get update || ! $sudo_cmd apt-get install -y nodejs npm; then
                        echo "setup-env.sh: apt-get install nodejs npm failed." >&2
                        echo "    On older Ubuntu/Debian, nodejs may not be in the default repos." >&2
                        echo "    Try NodeSource: https://github.com/nodesource/distributions" >&2
                        return 6
                    fi
                    return 0
                    ;;
                fedora|rhel|centos|rocky|almalinux|amzn)
                    echo "Detected RHEL/Fedora family (${ID}). Installing Node via dnf:"
                    echo "  sudo dnf install -y nodejs npm"
                    local sudo_cmd=""
                    [[ "$EUID" -ne 0 ]] && sudo_cmd="sudo"
                    if ! $sudo_cmd dnf install -y nodejs npm; then
                        echo "setup-env.sh: dnf install nodejs npm failed." >&2
                        return 6
                    fi
                    return 0
                    ;;
                arch|manjaro|endeavouros)
                    echo "Detected Arch family (${ID}). Installing Node via pacman:"
                    echo "  sudo pacman -S --noconfirm nodejs npm"
                    local sudo_cmd=""
                    [[ "$EUID" -ne 0 ]] && sudo_cmd="sudo"
                    if ! $sudo_cmd pacman -S --noconfirm nodejs npm; then
                        echo "setup-env.sh: pacman install nodejs npm failed." >&2
                        return 6
                    fi
                    return 0
                    ;;
                *)
                    echo "setup-env.sh: unknown Linux distro (${ID:-unknown})." >&2
                    echo "    Install Node manually: https://nodejs.org/en/download" >&2
                    echo "    Or use nvm: https://github.com/nvm-sh/nvm" >&2
                    return 6
                    ;;
            esac
        fi
        echo "setup-env.sh: /etc/os-release missing. Can't auto-detect package manager." >&2
        echo "    Install Node manually: https://nodejs.org/en/download" >&2
        return 6
    fi

    # Windows native (PowerShell / cmd). The Linux tools above won't apply.
    echo "setup-env.sh: this script can't auto-install Node on Windows." >&2
    echo "    Install via one of:" >&2
    echo "      - winget install OpenJS.NodeJS.LTS" >&2
    echo "      - choco install nodejs-lts" >&2
    echo "      - Download from https://nodejs.org/en/download" >&2
    return 6
}

# ─── Tool: reportgenerator ────────────────────────────────────────────
# Cross-platform install via `dotnet tool install -g
# dotnet-reportgenerator-globaltool`. verify.sh's coverage gate phase
# shells out to this to merge per-test-project cobertura XMLs into one
# deduplicated report — without it, the gate sees averages across test
# projects rather than the union of covered lines.
reportgenerator_check() {
    local bin
    if bin=$(command -v reportgenerator 2>/dev/null); then
        # `reportgenerator` requires args; the empty-args run prints
        # the usage banner ("Arguments / No report files specified" —
        # exact text varies by version). Probe by invoking it with
        # nothing and accepting any usage-shaped response.
        local out
        out="$("$bin" 2>&1 | head -3)"
        if [[ -n "$out" ]]; then
            echo "ready (PATH=$bin)"
            return 0
        fi
        echo "unrunnable ($bin is on PATH but produced no output)"
        return 1
    fi
    echo "missing"
    return 1
}

reportgenerator_install() {
    echo "─── reportgenerator ───"
    if ! command -v dotnet >/dev/null 2>&1; then
        echo "setup-env.sh: dotnet not found on PATH." >&2
        echo "    reportgenerator is a .NET global tool; install dotnet first." >&2
        echo "    See docs/devenv/setup.md." >&2
        return 2
    fi

    local existing
    if existing=$(command -v reportgenerator 2>/dev/null) && reportgenerator_check >/dev/null 2>&1; then
        echo "reportgenerator already installed: $existing"
        echo "Skipping install."
        return 0
    fi

    echo "Running: dotnet tool install -g dotnet-reportgenerator-globaltool"
    if ! dotnet tool install -g dotnet-reportgenerator-globaltool; then
        echo "setup-env.sh: dotnet tool install failed." >&2
        return 6
    fi

    # dotnet tool install writes to ~/.dotnet/tools by default. Surface
    # the user's hint if the binary isn't yet on PATH for this shell.
    if ! command -v reportgenerator >/dev/null 2>&1; then
        local tools_dir="${DOTNET_CLI_HOME:-$HOME/.dotnet}/tools"
        echo ""
        echo "Installed but not on PATH yet. Add to your shell rc:"
        echo "    export PATH=\"$tools_dir:\$PATH\""
    fi
}

# ─── Tool: gitleaks ────────────────────────────────────────────────────
# Defensive secret-scan layer. verify.sh's Phase 5c runs `gitleaks detect`
# against the local working tree on every --scope; failures mean a new
# secret has been introduced by the dev's branch and must be remediated
# before push. Mirrors .github/workflows/ci-fast.yml's defensive-layer
# convention (gitleaks + actionlint + cspell).
#
# brew has a maintained formula on macOS:
#   brew install gitleaks
# On Linux, the upstream release tarball is a single static binary that
# unpacks into /usr/local/bin/gitleaks — no runtime dependencies.
gitleaks_check() {
    local bin
    if bin=$(command -v gitleaks 2>/dev/null); then
        local ver
        ver=$("$bin" version 2>/dev/null || echo unknown)
        echo "ready (PATH=$bin, version=$ver)"
        return 0
    fi
    echo "missing"
    return 1
}

gitleaks_install() {
    echo "─── gitleaks ───"
    if command -v gitleaks >/dev/null 2>&1; then
        echo "gitleaks already installed: $(command -v gitleaks)"
        echo "Skipping install."
        return 0
    fi

    if is_macos; then
        echo "Detected macOS. Installing gitleaks via Homebrew:"
        echo "  brew install gitleaks"
        if ! command -v brew >/dev/null 2>&1; then
            echo "setup-env.sh: brew not found on PATH." >&2
            echo "    Install Homebrew first: https://brew.sh" >&2
            return 6
        fi
        if ! brew install gitleaks; then
            echo "setup-env.sh: brew install gitleaks failed." >&2
            return 6
        fi
        return 0
    fi

    if is_linux_native || is_wsl; then
        echo "Downloading gitleaks v8.18.4 binary (static, no deps):"
        local url="https://github.com/gitleaks/gitleaks/releases/download/v8.18.4/gitleaks_8.18.4_linux_x64.tar.gz"
        local tmp
        tmp=$(mktemp -d)
        if ! curl -sSfL "$url" | tar -xz -C "$tmp" gitleaks; then
            echo "setup-env.sh: failed to download gitleaks from $url" >&2
            rm -rf "$tmp"
            return 6
        fi
        if ! command -v sudo >/dev/null 2>&1 && [[ "$EUID" -ne 0 ]]; then
            echo "setup-env.sh: installing to /usr/local/bin requires root." >&2
            echo "    Re-run with sudo, or:" >&2
            echo "      sudo install -m 0755 '$tmp/gitleaks' /usr/local/bin/gitleaks" >&2
            rm -rf "$tmp"
            return 6
        fi
        local sudo_cmd=""
        [[ "$EUID" -ne 0 ]] && sudo_cmd="sudo"
        if ! $sudo_cmd install -m 0755 "$tmp/gitleaks" /usr/local/bin/gitleaks; then
            echo "setup-env.sh: install to /usr/local/bin failed." >&2
            rm -rf "$tmp"
            return 6
        fi
        rm -rf "$tmp"
        echo "Installed: /usr/local/bin/gitleaks"
        return 0
    fi

    echo "setup-env.sh: can't auto-install gitleaks on Windows." >&2
    echo "    Download from https://github.com/gitleaks/gitleaks/releases" >&2
    return 6
}

# ─── Tool: actionlint ─────────────────────────────────────────────────
# Workflow-file lint. verify.sh's Phase 5d runs `actionlint` against
# .github/workflows/*.yml/*.yaml; failures mean a malformed workflow
# step that would break CI dispatch. Mirrors
# .github/workflows/ci-fast.yml's defensive-layer convention.
#
# brew has a maintained formula on macOS:
#   brew install actionlint
# On Linux, the upstream release tarball unpacks into /usr/local/bin/.
actionlint_check() {
    local bin
    if bin=$(command -v actionlint 2>/dev/null); then
        local ver
        ver=$("$bin" -version 2>/dev/null || echo unknown)
        echo "ready (PATH=$bin, version=$ver)"
        return 0
    fi
    echo "missing"
    return 1
}

actionlint_install() {
    echo "─── actionlint ───"
    if command -v actionlint >/dev/null 2>&1; then
        echo "actionlint already installed: $(command -v actionlint)"
        echo "Skipping install."
        return 0
    fi

    if is_macos; then
        echo "Detected macOS. Installing actionlint via Homebrew:"
        echo "  brew install actionlint"
        if ! command -v brew >/dev/null 2>&1; then
            echo "setup-env.sh: brew not found on PATH." >&2
            echo "    Install Homebrew first: https://brew.sh" >&2
            return 6
        fi
        if ! brew install actionlint; then
            echo "setup-env.sh: brew install actionlint failed." >&2
            return 6
        fi
        return 0
    fi

    if is_linux_native || is_wsl; then
        # actionlint's latest release ships only the macOS tarball in
        # releases/tag; on Linux the install recipe is the official
        # download script. Pin to v1.7.7 (matches the rhysd/actionlint@v1.7.7
        # GitHub Action used in ci-fast.yml so the two linters are
        # bit-identical when they disagree on a rule).
        echo "Installing actionlint v1.7.7 (matches rhysd/actionlint@v1.7.7 in CI):"
        local url="https://raw.githubusercontent.com/rhysd/actionlint/1.7.7/scripts/download-actionlint.bash"
        local tmp
        tmp=$(mktemp -d)
        if command -v sudo >/dev/null 2>&1 || [[ "$EUID" -eq 0 ]]; then
            local sudo_cmd=""
            [[ "$EUID" -ne 0 ]] && sudo_cmd="sudo"
            if ! $sudo_cmd bash -c "curl -sSfL '$url' | bash -s -- 1.7.7"; then
                echo "setup-env.sh: official actionlint install script failed." >&2
                rm -rf "$tmp"
                return 6
            fi
        else
            echo "setup-env.sh: installing actionlint to /usr/local/bin requires root." >&2
            echo "    Re-run with sudo, or pipe the official install script manually:" >&2
            echo "      curl -sSfL '$url' | bash -s -- 1.7.7" >&2
            rm -rf "$tmp"
            return 6
        fi
        rm -rf "$tmp"
        echo "Installed actionlint."
        return 0
    fi

    echo "setup-env.sh: can't auto-install actionlint on Windows." >&2
    echo "    Download from https://github.com/rhysd/actionlint/releases" >&2
    return 6
}

# ─── Dispatch ──────────────────────────────────────────────────────────
run_check() {
    local missing=()
    local failed=()
    for tool in "${REQUESTED[@]}"; do
        local result
        if ! result=$("${tool}_check" 2>&1); then
            missing+=("$tool")
            failed+=("$result")
        else
            echo "[ok] $tool: $result"
        fi
    done
    if [[ ${#missing[@]} -gt 0 ]]; then
        echo ""
        echo "setup-env.sh --check: missing: ${missing[*]}" >&2
        for i in "${!missing[@]}"; do
            echo "  - ${missing[$i]}: ${failed[$i]}" >&2
        done
        echo "" >&2
        echo "Run without --check to install:" >&2
        echo "  $0 ${missing[*]}" >&2
        return 5
    fi
    return 0
}

run_install() {
    local rc=0
    for tool in "${REQUESTED[@]}"; do
        "${tool}_install"
        local tool_rc=$?
        if [[ $tool_rc -ne 0 ]]; then
            rc=$tool_rc
            # Stop on first failure — partial setups are confusing.
            break
        fi
    done
    return $rc
}

if [[ "$CHECK_MODE" -eq 1 ]]; then
    run_check
    exit $?
fi

run_install
rc=$?

if [[ $rc -eq 0 ]]; then
    echo ""
    echo "─── Summary ───"
    for tool in "${REQUESTED[@]}"; do
        if status=$("${tool}_check" 2>&1); then
            echo "  [ok]  $tool: $status"
        else
            echo "  [!!]  $tool: $status"
        fi
    done
fi

exit $rc
