#!/usr/bin/env bash
# install-ci-pre-push-hook.sh — installs ci-pre-push-hook.sh as
# .git/hooks/pre-push so `git push` runs the Studywise local CI mirror
# (verify.sh) before allowing the push.
#
# Usage:
#   ./scripts/install-ci-pre-push-hook.sh                  # install in current repo
#   ./scripts/install-ci-pre-push-hook.sh /abs/path/to/repo  # install in another repo
#   ./scripts/install-ci-pre-push-hook.sh --uninstall       # remove the hook
#   ./scripts/install-ci-pre-push-hook.sh --help
#
# Exit codes:
#   0   hook installed (or already installed + refreshed)
#   1   install failed (permissions, no .git, etc.)
#   2   invalid arguments
#
# Behaviour:
#   - Idempotent: re-running refreshes the hook from the latest source.
#   - If a non-managed hook already exists at .git/hooks/pre-push (one
#     not installed by this script), it is moved aside to
#     .git/hooks/pre-push.user-backup-<timestamp>.
#   - The --uninstall flag restores that backup (if found) so a user
#     who previously had a different pre-push workflow can roll back.
#   - Worktrees: each worktree has its own .git/hooks/ but the hook is
#     typically symlinked or stored at <main-repo>/.git/hooks. This
#     script installs into the worktree's resolved gitdir so the hook
#     applies to that worktree's pushes.

set -uo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
HOOK_SOURCE="$SCRIPT_DIR/ci-pre-push-hook.sh"
MANAGED_MARKER="ci-pre-push-hook.sh"

usage() {
    cat <<EOF
Usage: $0 [target-repo]
       $0 --uninstall [target-repo]
       $0 --help

Installs (or refreshes) scripts/ci-pre-push-hook.sh as the target
repo's .git/hooks/pre-push. With no arguments, installs in the
current working directory's repo.

The hook runs scripts/verify.sh --scope=fast before allowing a push.
The --scope=full heavy check runs separately via the PR Full Gate
on request-full-ci — pre-push is the dev-inner-loop gate, not a
replacement for the Full Gate.

Bypass with:
    SKIP_CI_PRE_PUSH=1 git push

Options:
  --uninstall     Remove the hook (restoring any user-backup).
  -h, --help      Show this help.

Examples:
  $0                                    # install in current repo
  $0 ~/worktrees/feature-foo            # install in a worktree
  $0 --uninstall                        # remove hook from current repo

See scripts/README.md for full context.
EOF
}

# ─── Argument parsing ──────────────────────────────────────────────────
UNINSTALL=0
TARGET="."

while [[ $# -gt 0 ]]; do
    case "$1" in
        --uninstall)    UNINSTALL=1; shift ;;
        -h|--help)      usage; exit 0 ;;
        -*)             echo "❌ Unknown flag: $1" >&2; usage >&2; exit 2 ;;
        *)              TARGET="$1"; shift ;;
    esac
done

# ─── Validate hook source ──────────────────────────────────────────────
if [[ ! -f "$HOOK_SOURCE" ]]; then
    echo "❌ Hook source not found: $HOOK_SOURCE" >&2
    exit 1
fi

# ─── Resolve target repo ───────────────────────────────────────────────
TARGET="$(cd "$TARGET" && pwd 2>/dev/null || echo "$TARGET")"
if [[ ! -d "$TARGET/.git" && ! -f "$TARGET/.git" ]]; then
    echo "❌ Not a git repo: $TARGET" >&2
    exit 1
fi

# Resolve gitdir (handles worktrees where .git is a file pointing at
# the main repo's .git/worktrees/<branch>/).
GITDIR="$(cd "$TARGET" && git rev-parse --git-dir 2>/dev/null || echo "")"
if [[ -z "$GITDIR" ]]; then
    echo "❌ Failed to resolve gitdir for: $TARGET" >&2
    exit 1
fi
[[ "$GITDIR" != /* ]] && GITDIR="$TARGET/$GITDIR"
HOOK_TARGET="$GITDIR/hooks/pre-push"
mkdir -p "$GITDIR/hooks"

# ─── Uninstall path ────────────────────────────────────────────────────
if [[ "$UNINSTALL" == "1" ]]; then
    if [[ ! -f "$HOOK_TARGET" ]]; then
        echo "No pre-push hook at $HOOK_TARGET — nothing to uninstall."
        exit 0
    fi
    if grep -q "$MANAGED_MARKER" "$HOOK_TARGET" 2>/dev/null; then
        rm -f "$HOOK_TARGET"
        echo "✅ Removed pre-push hook: $HOOK_TARGET"
        # Look for a user-backup file to restore.
        BACKUP="$(ls -t "$GITDIR/hooks/pre-push.user-backup-"* 2>/dev/null | head -1 || true)"
        if [[ -n "$BACKUP" ]]; then
            mv "$BACKUP" "$HOOK_TARGET"
            chmod +x "$HOOK_TARGET"
            echo "↪ Restored user backup: $BACKUP → $HOOK_TARGET"
        fi
        exit 0
    fi
    echo "⚠️  Pre-push hook at $HOOK_TARGET is not managed by this installer." >&2
    echo "    Leaving it untouched. To replace it, remove manually first." >&2
    exit 1
fi

# ─── Install / refresh path ────────────────────────────────────────────
# Back up an existing non-managed hook.
if [[ -f "$HOOK_TARGET" ]] && ! grep -q "$MANAGED_MARKER" "$HOOK_TARGET" 2>/dev/null; then
    BACKUP="$HOOK_TARGET.user-backup-$(date +%Y%m%d-%H%M%S)"
    mv "$HOOK_TARGET" "$BACKUP"
    echo "↪ Backed up existing pre-push hook to: $BACKUP"
fi

if cp "$HOOK_SOURCE" "$HOOK_TARGET"; then
    chmod +x "$HOOK_TARGET"
    echo "✅ Installed pre-push hook: $HOOK_TARGET"
    echo "    Next 'git push' will run scripts/verify.sh --scope=fast."
    echo "    Heavy --scope=full runs via PR Full Gate (request-full-ci label)."
    echo "    Bypass (use sparingly): SKIP_CI_PRE_PUSH=1 git push"
    exit 0
fi

echo "❌ Failed to install pre-push hook: $HOOK_TARGET" >&2
exit 1
