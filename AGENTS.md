# AGENTS.md — Working agreement for AI agents on this repo

## Response style

Default to short and direct. One-liners for confirmations, decisions, and
tradeoffs. Multi-paragraph answers only when the user explicitly asks for
detail, OR the topic is genuinely complex (cross-repo plumbing, non-obvious
failure mode). When unsure, short is the safer choice — the user can ask to
expand.

## Where work happens

- The main, working branch is `main` checked out in this repo.
- All non-trivial work MUST happen in a **git worktree**, not directly on
  `main`.
- Create the worktree from up-to-date `main`:
  ```bash
  git fetch origin && git checkout main && git reset --hard origin/main
  git worktree add ../studywise-cli/<repo>-<NN>-<slug> -b chore/<NN>-<slug> main
  ```
- **Worktree paths in this repo go inside `studywise-cli/`**, not at the
  parent level — i.e. `../studywise-cli/studywise-cli-32-local-ci`, not
  `../studywise-cli-32-local-ci`. This repo shares the parent with
  `studywise-api` and `studywise-app`; nested under `studywise-cli/`
  keeps each repo's worktrees grouped.
- **After creating the worktree, stop and ask the user to move the harness**
  (change the agent's working directory) to the new worktree path before
  continuing with any edits, `git add`, or `git commit`. `git worktree add`
  creates a separate working tree on disk but does NOT switch the agent's
  working directory — subsequent Bash, Read, Edit, and `git` invocations
  will still target `main` until the harness is moved. Verify the move
  with `git rev-parse --show-toplevel` before continuing; if it does not
  print the new worktree path, stop and ask rather than guessing.
- **Immediately push the empty branch to origin** (`git push -u origin
  <branch>`) so it survives local-machine death, worktree corruption, or
  long context-switches. Repeat `git push` at the end of every work session.
- Do ALL edits, commits, and verification inside the worktree. Never commit
  on `main` directly.
- When work is complete and verified, push the branch one last time and
  open the PR from there (`gh pr create --base main`).
- Clean up with `git worktree remove ../studywise-cli/<worktree-name>` after
  the PR merges.

## What "done" means

Two local runs gate the work:

1. **Pre-push (fast):** the pre-push hook
   (`./scripts/install-ci-pre-push-hook.sh`, install once per repo)
   runs `./scripts/verify.sh --scope=fast` on every push. Don't bypass
   it (no `SKIP_CI_PRE_PUSH=1`).
2. **Post-PR (full):** after `gh pr create`, run
   `./scripts/verify.sh --post-comment` (the default `--scope=full`)
   and wait for it to pass. The comment is the artifact on the PR — no
   work is "done" until that comment lands green.

CLI specifics layered on top of the contract above (don't override it,
just adapt the right CLI knobs):

- `--scope=fast` here == `--scope=full`. The CLI uses NUnit 5 with
  `[Category("Unit")]` and `[Category("Integration")]` markers, but no
  `[Category("LongRunning")]` tests exist today. If a LongRunning test
  ever lands, `--scope=fast` will gain the standard
  `--filter "Category!=LongRunning"` to diverge from `--scope=full`.
- No E2E phase. Per the org test strategy, E2E belongs in the frontend
  repo; the CLI's two layers are Unit + Integration. Integration tests
  build a WireMock server in-process and never dial the real Studywise
  API, so no `STUDYWISE_API_KEY` secret is needed for any local or CI
  run. See `docs/testing/testing-strategy.md`.
- If verify.sh can't run locally (missing tool, network issue), STOP
  and tell the user — don't substitute `dotnet test <one-project>`.
  `./scripts/setup-env.sh` fixes most local blockers.

## Plan documentation

**Default is no plan.** Don't create a plan doc unless one of these is true:

- The user explicitly asks for a plan (or for a plan doc under `docs/plans/`).
- The work is an epic — multi-step, multi-session, expected to span
  multiple PRs — where the plan tracks scope across the whole effort.

If a plan doc is warranted, load `feature-planning` (for new feature
work) or `start-issue` (for issue work) — they own the plan-doc format
and workflow. Everything else (single PRs, small fixes, obvious work)
skips the plan doc and goes straight to implementation.

## Commit & merge policy

Small conventional commits, one logical change each. Push the branch
frequently (`git push origin <branch>`). PRs squash-merge into `main` —
save polish for the PR title/description. Revert with
`git revert -m 1 <merge-hash>`.

## Three-tier boundaries

- ✅ **Always do:** Work in a worktree; run `verify.sh` before push
  on non-docs diffs (the pre-push hook handles this); capture plan
  docs under `docs/plans/` for multi-step work; re-capture issue
  snapshots of live state before acting on them; capture a
  retrospective as a follow-up PR when a work PR had user corrections.
- ⚠️ **Ask first:** Before deleting or renaming files used by other
  modules; before changing CI workflow files (`.github/workflows/*.yml`)
  or auth/secrets handling; before merging `main` into a long-lived
  branch; before bypassing a failing test.
- 🚫 **Never:** Push directly to `main` or any branch with protection
  rules; edit `node_modules/`, build outputs, generated files; commit
  secrets; substitute `dotnet test <one-project>` for
  `./scripts/verify.sh`; bundle a retrospective into its work PR.

## AI agent behaviour — discuss before big-axe edits

Back-and-forth "do it then revert it" cycles cost more time than getting
the analysis right upfront.

- **Lay out trade-offs before editing** when a change touches more than
  ~3 files OR removes workflow structure (matrix, sharding, concurrency,
  deploys, gating, CI workflows). Write the analysis in one message
  (cost/benefit, what changes, what to revert to) and stop. Wait for
  "do it" / "yes" / a chosen option before any edit. This applies even
  when plan mode has been exited — the user reading the analysis is the
  deliverable, not the file change.
- **No "do it then revert it" without an explicit request.** If a prior
  commit on the worktree branch looks wrong, write up the disagreement
  (cite the commit hash + a one-line reason) and let the user pick.
  Don't unilaterally revert an earlier commit you made in the same session.
- **When the user corrects the framing**, that correction IS the answer.
  Don't keep defending the original analysis; update the plan and
  re-confirm.
- **One branch, one PR, one direction per session.** If a session flips
  the same decision twice, pause and ask whether the analysis actually
  changed or whether you're getting confused.

## CI troubleshooting

For any CI investigation (PR not running, CI red, `BLOCKED` after green,
or pre-merge deploy verification), load
`.opencode/skills/ci-troubleshooting/SKILL.md`.

When an issue body quotes a timestamped snapshot of live state (Azure
config, env vars, infra inventory), re-run the snapshot query just
before acting on it — see `.opencode/skills/re-capture-issue-snapshots/SKILL.md`.

## Self-Improvement Loop

Work PR with user corrections → follow-up retrospective PR (template
+ naming in `docs/retrospective/README.md`). Retrospective is its own
PR — never bundled into the work PR.

## Task Management

1. **Plan First** for non-trivial work (3+ steps, architectural decisions,
   cross-module effects).
2. **Verify Plan** — check in with the user before starting implementation.
3. **Track Progress** with the todo list (`todowrite`), updated in real time.
   Mark items `completed` only after the required work — including
   verification — is actually done.
4. **Explain Changes** with a high-level summary at each step.
5. **Document Results** — capture the plan + retrospective trail.
6. **Capture Retrospective** as a follow-up PR after the work PR merges
   (see "Self-Improvement Loop").
