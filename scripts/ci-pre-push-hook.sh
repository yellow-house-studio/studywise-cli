#!/usr/bin/env bash
# ci-pre-push-hook.sh — git pre-push hook body installed by
# scripts/install-ci-pre-push-hook.sh. NOT meant to be invoked directly;
# it's copied to <repo>/.git/hooks/pre-push and runs automatically on
# `git push`.
#
# Runs verify.sh --scope=fast (~3-6 min) before allowing a push. The
# --scope=full heavy validation is run separately via the PR Full Gate
# (request-full-ci label) when reviewers want the deep check — local
# pre-push only catches the fast feedback path.
#
# Skip via env var for emergency pushes:
#   SKIP_CI_PRE_PUSH=1 git push
# Bypasses the fast gate. The escape hatch is documented in
# scripts/README.md and AGENTS.md. Use sparingly — the pre-push gate
# catches failures that would otherwise burn CI runner time.

set -uo pipefail

# Skip when explicitly requested. Documented escape hatch — see
# scripts/README.md and AGENTS.md.
if [[ "${SKIP_CI_PRE_PUSH:-0}" == "1" ]]; then
    echo "[ci-pre-push] SKIP_CI_PRE_PUSH=1 — bypassing pre-push gates."
    exit 0
fi

# Resolve the hook's repo root. The hook runs from the worktree; the
# CI scripts live at <repo-root>/scripts/. `git rev-parse --show-toplevel`
# works from any worktree, returning the main repo's absolute path.
REPO_ROOT="$(git rev-parse --show-toplevel 2>/dev/null || echo "")"
if [[ -z "$REPO_ROOT" ]] || [[ ! -x "$REPO_ROOT/scripts/verify.sh" ]]; then
    echo "[ci-pre-push] ❌ Could not locate scripts/verify.sh at $REPO_ROOT/scripts/" >&2
    echo "    (this hook is installed via scripts/install-ci-pre-push-hook.sh)" >&2
    exit 1
fi

cd "$REPO_ROOT" || exit 1

echo "[ci-pre-push] ═══ Running verify.sh --scope=fast ═══"
if ! "$REPO_ROOT/scripts/verify.sh" --scope=fast; then
    echo "[ci-pre-push] ❌ verify.sh --scope=fast failed." >&2
    echo "    Fix the failures above, then push again." >&2
    echo "    To bypass (use sparingly): SKIP_CI_PRE_PUSH=1 git push" >&2
    exit 1
fi

echo "[ci-pre-push] ✅ Pre-push gate passed."
exit 0

