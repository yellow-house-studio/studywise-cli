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

Before pushing a PR that triggers a remote CI gate, the local CI scripts
must pass. They mirror the remote gates and exist precisely because unit
tests don't catch things that break the wider CLI surface (format drift,
vulnerable transitive packages, missing coverage on new code).

- **`./scripts/verify.sh` is the single local entry point.** Mirrors the
  full `.github/workflows/ci-fast.yml`, `ci-full.yml`,
  `ci-production.yml`, and `update-baseline-on-merge.yml` surface. Two
  scopes cover what the agent needs:
  - `./scripts/verify.sh --scope=unit` — restore + Release build (with
    `TreatWarningsAsErrors` + `EnforceXmlDocs`) + unit tests only. ~1-3
    min. Use this for the dev inner loop on tests-only diffs.
  - `./scripts/verify.sh` (default `--scope=full`) — heavy full suite:
    unit + integration + e2e (when `STUDYWISE_API_KEY` is set) +
    format + analyzers + gitleaks + actionlint + cspell + security
    scan + coverage merge + per-assembly coverage gate + baseline
    auto-raise. ~5-15 min. Run this before pushing a non-docs diff.
  - `./scripts/verify.sh --scope=integration` — unit + integration
    (no e2e). Useful when Dev Proxy / `STUDYWISE_API_KEY` aren't
    available locally.
  - CLI's `--scope=fast` is identical to `--scope=full` — there is no
    Fast/LongRunning category split (xunit doesn't have the attribute
    pattern NUnit uses).
- **Default error mode is fail-fast** — the first broken phase
  short-circuits the run. Pass `--no-fail-fast` to collect all
  failures and exit with the aggregate code (useful for diagnostic
  sweeps or CI runs where you want the full picture).
- **Do NOT substitute `dotnet test` for the script.** The script
  adds the `TreatWarningsAsErrors` build flags, the
  per-project `TestResults/Verify/` layout, the coverage merge +
  gate, and the format/analyzers/gitleaks/actionlint/cspell/security
  gates that `dotnet test <one-project>` misses.
- **`verify.sh` cannot be waived by scope.** No "I only touched
  tests", "blast radius is small", or "the script times out locally"
  exception. If the script cannot complete locally, STOP and tell the
  user — don't substitute `dotnet test --filter` and don't trigger
  remote CI anyway. (`./scripts/setup-env.sh` fixes most local
  blockers.)
- **Docs-only carve-out:** diffs whose only changed files match
  `*.md`, `docs/**`, `*.txt`, `.cursor/skills/**`, `.opencode/skills/**`,
  `.codex/skills/**` don't need the script. Any diff that touches
  `*.cs`, `*.yml`, `*.json`, `*.ps1`, or `*.sh` MUST run the script
  first. The carve-out is mechanical; don't extend it by judgment.
- **Reading the failure summary.** When `verify.sh` fails, the
  script already prints the failing project + failing test names +
  first line of each error (parsed from the TRX file under
  `TestResults/Verify/`). Every stage writes `TestResults/<stage>/console.log`
  (full verbose xunit output, grep-able) and
  `TestResults/<stage>/stderr.log` so a failed test is inspectable
  without re-running. The CI workflow (`ci-full.yml`) archives
  `TestResults/` on failure (3-day retention). Agents should not
  need to re-run with a custom `--filter` to identify which tests
  failed. If the printed summary is insufficient, file an issue
  referencing `scripts/lib/test-report.sh` before adding workarounds
  — do NOT substitute `dotnet test <one-project>` as a debugging
  step (that's the carve-out `verify.sh` is meant to prevent).
- **E2E without `STUDYWISE_API_KEY`.** Under `--scope=full`, the
  pre-flight blocks before any phase runs if `STUDYWISE_API_KEY`
  isn't in the shell or `~/.secrets/studywise-cli.env`. Under
  `--scope=unit`/`--scope=integration` the E2E phase is skipped
  cleanly (not a failure) when the key is absent. See
  `docs/devenv/setup.md` for setup.
- **Tooling prerequisite.** `verify.sh`'s coverage-merge phase
  (Phase 6.5) shells out to `reportgenerator` (the .NET global tool
  `dotnet-reportgenerator-globaltool`). Without it the gate sees
  average coverage across test projects rather than the union.
  Install with `./scripts/setup-env.sh reportgenerator` (auto-detects
  across macOS / Linux / Windows) or
  `dotnet tool install -g dotnet-reportgenerator-globaltool`. After
  install, `~/.dotnet/tools` may need to be on PATH —
  `export PATH="$HOME/.dotnet/tools:$PATH"`. The script fails fast
  with a fix-it pointer if reportgenerator isn't in PATH.
- **Defensive layers.** gitleaks (secret scan), actionlint (workflow
  lint), and cspell (source spell check) are auto-skipped when their
  tool isn't on PATH — they don't block devs who haven't bootstrapped
  the optional tooling. The skip flag (`--skip-gitleaks`,
  `--skip-actionlint`, `--skip-cspell`) is the explicit opt-out for
  CI runners.

## Plan documentation

Work that takes more than a line of thought — multi-step, multi-commit,
or multi-file — must be captured in a plan document under `docs/plans/`:

- **Feature work** → `docs/plans/feature-plans/issue-<N>-<slug>.md`
- **Issue work**  → `docs/plans/issue-<N>-<slug>.md`

Plan docs are committed as the first commit of the PR they belong to and
updated as the work evolves (amend in place; don't fragment across `v2`
files). PR descriptions reference the plan; they do not replace it.

## Commit & merge policy

- **Small commits, often.** One commit per logical change. Conventional
  Commits: `type(scope): subject`. Stage only files that belong to that
  change.
- **Push the worktree branch frequently.** After every commit (or at most
  every few) run `git push origin <branch>`. The branch should track on
  the remote, not sit locally between sessions.
- **PRs are squash-merged into `main`.** One commit per PR regardless
  of how many logical commits the branch contained. Save polish for the
  PR title + description; don't agonise over per-commit messages on a
  branch the squash will collapse.
- **Reverting:** `git revert -m 1 <merge-commit-hash>` (the `-m 1` keeps
  the mainline parent `main`).
- **Branch cleanup:** delete the source branch after squash merge (GitHub
  offers this in the merge dialog).
- **No "fix typo" follow-ups land on the original PR.** The squash makes
  any follow-up the only signal of the fix on `main`.
- **Never commit secrets** (`.env`, anything from `~/.secrets/`,
  `STUDYWISE_API_KEY` values, Auth0 client secrets).

## Three-tier boundaries

- ✅ **Always do:** Work in a worktree; run `verify.sh` before push
  on non-docs diffs; capture plan docs under `docs/plans/` for multi-step
  work; re-capture issue snapshots of live state before acting on them;
  capture a retrospective as a follow-up PR when a work PR had user
  corrections. **Pre-push hook (`./scripts/install-ci-pre-push-hook.sh`)
  is the canonical validation path** — install it once per repo; it
  runs `verify.sh --scope=fast` on every push. Bypass with
  `SKIP_CI_PRE_PUSH=1 git push` only as a last resort and document the
  bypass in the PR description.
- ⚠️ **Ask first:** Before deleting or renaming files used by other
  modules; before changing CI workflow files (`.github/workflows/*.yml`)
  or auth/secrets handling; before merging `main` into a long-lived
  branch; before bypassing a failing test.
- 🚫 **Never:** Push directly to `main` or any branch with protection
  rules; edit `node_modules/`, build outputs, generated files; commit
  secrets; substitute `dotnet test <one-project>` for
  `./scripts/verify.sh`; bundle a retrospective into its work PR.

## AI agent behaviour — discuss before big-axe edits

Back-and-frittes "do it then revert it" cycles cost more time than getting
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

## Re-capture issue snapshots of live state

When an issue body quotes a snapshot of live state (NuGet package list,
GitHub Actions config, Auth0 config) with a timestamp, **re-run that
snapshot query just before acting on it**. Quote the fresh snapshot in the
PR body, not the stale one. If the state has drifted, call that out
explicitly in the PR description.

## Self-Improvement Loop

After a work PR merges, capture corrections in a retrospective under
`docs/retrospective/`. Templates, naming, topical index, and trigger rule
live in `docs/retrospective/README.md`.

- **Trigger:** a work PR has merged to `main` AND had at least one user
  correction during the work.
- **Feature retrospective** (default) → `docs/retrospective/feature-<slug>.md`.
  Use this for any merged work PR with user corrections. The PR
  description is the entry point — feature retrospectives are how we
  document corrections in the form the next agent can act on. The
  template (Validation Checklist, What Went Well / Didn't, Metrics)
  exists to keep retrospectives consistent across PRs.
- **Bundling rule:** the retrospective is a **follow-up PR after the work
  PR merges**. Don't bundle the retrospective into the work PR — mid-flight
  lessons get stale; review cycles churn; the squash collapses them anyway.
- When an `opencode` skill would change the next agent's behaviour (a
  pattern fires in a specific situation and the agent needs to do
  something different next time), install the skill in
  `~/.config/opencode/skills/<name>/SKILL.md`. Lessons are retrospective;
  behaviour modifiers are skills. Different layers, both valid.

## Never Bypass Branch Protection

- **NEVER push directly to protected branches** (`main`, or any branch
  with protection rules).
- **ALWAYS create pull requests** for changes to protected branches.
- If a direct push is attempted and somehow succeeds, **revert immediately**
  and redo the change via PR.
- This applies even if you technically have admin privileges or the bypass
  appears to succeed.
- When in doubt: ask the user instead of bypassing protection.

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