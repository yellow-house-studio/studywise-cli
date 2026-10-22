---
tags: [sdk, nuget, github-packages, ci, release, secrets-handling, cross-repo, verification]
---

# Retrospective: SDK adoption (issue #31)

> **Issue:** #31
> **Closing PRs:** #47, #49, #50, #51, #52, #53, #54, #56
> **Date:** 2026-10-22

## Outcome

Merged. The CLI consumes `YellowHouseStudio.Studywise.Sdk` instead of its own
hand-rolled transport, credential store and API-key handler. The SDK is
pinned at 2.3.0, restored from the org's private GitHub Packages feed, with
the feed registered in CI from a repository secret. `studywise auth verify`
is the first user-facing command built on the SDK. The release pipeline,
which had been failing daily since 2026-10-17 without producing a single
release, can pack its NuGet package again.

Two things remain open on the API side and are not this repo's to fix:
studywise-api#890 (a primary-handler hook in `AddStudywiseClient`, so the
SDK itself stops the API key leaking across a redirect), #891 (a
`HealthClient`), #907 (a `CountryClient`), #908 (a `HomeworkClient`), and
#909 (the SDK publish has still never run through the release path).

## What we set out to do

Use the SDK the API repo publishes instead of duplicating it in the CLI, per
`docs/design/cli-design.md` § 7, and unblock the CLI's own feature work
(`auth verify`, education levels) that the SDK surface enables.

## What actually happened

Six merged PRs and three issues filed, plus one release-pipeline bug found
on the way:

- **#47** — the consumption guide (`docs/architecture/sdk-consumption.md`).
- **#49** — Central Package Management. CPM forced the repo-level
  `nuget.config` into the same PR: with more than one package source and no
  mapping, CPM raises NU1507, which is an error here.
- **#50** — the SDK dependency plus CI feed registration. Split out from the
  swap so that a credential or mapping failure could not block the refactor.
  CI proved it end to end on a cache miss, so the package was genuinely
  downloaded from the private feed.
- **#51** — the transport swap itself. Five source files deleted, the
  `auth-verify` check rewired onto `AuthClient`, and the redirect hardening
  preserved via a CLI-side re-registration of the SDK's client.
- **#52** — the `connection` diagnostic was probing `/health`, which the API
  does not map. Found while reading the API's route table; the WireMock
  stubs had been asserting the path the CLI expected rather than the path the
  API serves.
- **#53** — the README, which advertised four commands when only one
  existed, and pointed its download instructions at a release that was never
  published.
- **#54** — `studywise auth verify`, the first command to talk to the API.
- **#56** — the release pipeline's `dotnet pack` had been failing every day
  since 2026-10-17 with NU5104 (a stable package may not depend on a
  prerelease — the CLI depends on `System.CommandLine 2.0.0-beta4`). No
  release had ever been produced. Fixed with a scoped suppression, plus a
  package-check phase in `verify.sh` so the class of breakage cannot sit
  undetected again.

The corrections below are the ones that changed the work's direction.

## Corrections

### A 404 from a package feed is not evidence of absence
- **Pattern:** Probing whether a package version exists on a private feed.
- **The mistake:** The GitHub Packages flatcontainer returned 404 for
  `yellowhousestudio.studywise.sdk` — authenticated and unauthenticated. That
  was read as "the package has never been published", and the conclusion was
  built on for hours: an issue asserting it, a plan assuming it, and a
  "corrected" commit in the consumption guide retracting the claim. The
  package had been published since 2026-10-21T09:17:44Z and restored in
  177 ms. The flatcontainer simply does not serve this private package; the
  feed's version index (`/download/<id>/index.json`) answers `200` with
  `{"versions":["2.3.0"]}`, and an actual `dotnet restore` succeeds.
- **The rule:** Before asserting that a package version does not exist,
  (1) query the feed's version index with an authenticated request, and
  (2) run the restore. A 404 from an endpoint that is not the version index
  says nothing about existence. Absence claims are expensive to retract.
- **Applies when:** Checking whether a package exists on GitHub Packages,
  Azure Artifacts, or any private NuGet source; or reading a feed that
  returns 404 or 401.

### A tool's exit code is not the answer — do the real operation
- **Pattern:** Verifying that a set of branches merges cleanly in any order.
- **The mistake:** `git merge-tree --write-tree` exited 0 for a merge that
  genuinely conflicted, and that was reported to the user as "both merge
  orders clean". A real clone with real merges then produced
  `CONFLICT (content): Merge conflict in README.md`. The two conflicting
  PRs were reported as fine for some time afterwards.
- **The rule:** A dry run or a convenience tool's exit status is not a
  result. Perform the real operation — clone, merge, read the output — before
  reporting that something is clean. This is the same shape as the
  feed-probe mistake: a proxy for the thing, mistaken for the thing.
- **Applies when:** Reporting on merge conflicts, build success, test
  results, or any check performed by a tool other than the one that does the
  work.

### Another team's obligation to us is the surface we consume
- **Pattern:** Filing an issue in `studywise-api` about their release
  pipeline.
- **The mistake:** The SDK's release-driven publish had never run, and
  `2.4.0` was staged in their csproj but unpublished. That was filed as
  studywise-api#909 with the framing "the SDK is frozen and consumers are
  waiting" — which was not true. The CLI ships on 2.3.0 today, and the
  redirect hardening has a working CLI-side workaround, so nothing in the
  CLI was blocked by it. The issue was theirs to fix, but not on our behalf,
  and the stakes were overstated.
- **The rule:** When another team owes us something, the ask is the surface
  we consume — here, an SDK client or a DI hook. Their internal pipeline,
  versioning process or tooling is their business; if it is broken, say so
  once, accurately, without inflating it into our dependency. The
  distinction matters because an inflated ask is harder for them to
  prioritise than a precise one.
- **Applies when:** Filing a cross-repo issue about a build, release or
  deployment problem in a repo you do not own.

### Lead with verified state, not a prediction
- **Pattern:** Writing a bug report about a version-computation failure.
- **The mistake:** The headline of studywise-api#909 was "the next release
  will fail at Version Bump — tag v6.39.0 exists but points at a commit
  that is not in master's history". The prediction happened to be right, but
  asserting a specific computed version is overclaiming: it depends on a
  third-party tool's behaviour, and a reader cannot tell the prediction from
  the observation. The real defect — an orphaned tag on origin that the
  version computation, which only sees tags reachable from HEAD, cannot
  account for — is verifiable on its own.
- **The rule:** Lead with what was verified. Put predictions in a note, and
  say what they depend on.
- **Applies when:** Writing a bug report, an issue body, or a PR description
  whose subject is the output of a tool or a future run.

### Never print a credential-bearing config into tool output
- **Pattern:** Reading a NuGet config that holds a PAT in order to
  authenticate a request.
- **The mistake:** `cat ~/.nuget/NuGet/NuGet.Config` was piped through `sed`
  to redact the password. The redaction pattern matched `Password="` but the
  attribute is `ClearTextPassword`, so the full token was printed into the
  tool result and into the session transcript. The token carries
  `read:packages`, `read:project` and `repo`.
- **The rule:** Extract the value into a variable and use it; never let a
  credential-bearing file reach stdout or a tool result. If a file must be
  displayed, redact by attribute name rather than by pattern, and read the
  output before pasting it.
- **Applies when:** Reading `~/.nuget/NuGet/NuGet.Config`, `.env` files,
  `~/.config/**` files, keychain exports, or any file that holds a token.
- **Note:** The same failure mode was already captured in another repo's
  retrospective for the same class of secret — in a private repo, so the
  citation is not linkable from this public one. It was repeated because
  this repo had no retrospective index to point at it. That is why
  `docs/retrospective/` now exists here, and why this repo keeps its own
  convention instead of deferring to another repo's.

## Cross-cutting implications

- The `X-Studywise-Api-Key` header reached a redirect target host when the
  SDK's client followed a 3xx. The CLI works around it by re-registering the
  SDK's client; the durable fix belongs in the SDK (studywise-api#890), and
  the same class of problem will recur for every consumer of the SDK that
  attaches a credential to every request.
- The SDK's `AddStudywiseClient` offers no hook for default request
  headers either, so the CLI's User-Agent rides along on the same
  re-registration. That was noted on studywise-api#890 rather than filed
  separately, because it is the same surface.
- `System.CommandLine` is pinned to a beta for a reason nobody wrote down.
  It is the reason the release pipeline could not pack, and the migration
  to GA is a breaking API change that removes two abstractions the CLI's
  command-handler pattern depends on (studywise-cli#55).

## Actions taken

- [x] Added a package-check phase to `scripts/verify.sh` so the release
      pipeline's `dotnet pack` is exercised on every PR — PR #56.
- [x] Suppressed NU5104 so the NuGet package can be produced, with a comment
      pointing at the real fix — PR #56.
- [x] Tracked the `System.CommandLine` GA migration, with the verified
      before/after API mapping — issue #55.
- [x] Filed the SDK surface gaps the CLI is waiting on — studywise-api#890,
      #891, #907, #908.
- [x] Filed the API release-path defect, reframed as a plain bug report
      rather than a CLI dependency — studywise-api#909.
- [x] Set the `NUGET_GITHUB_PACKAGES_TOKEN` repository secret on this repo
      (2026-10-21) — the PAT's scope is org-wide, and Actions secrets are
      per-repository, so the token has one copy per consuming repo.
- [x] Added the verification rules to this repo's AGENTS.md, and created
      `docs/retrospective/` with an index, so the lessons are reachable from
      here.
- [x] **Rotate the GitHub Packages PAT** — the procedure is
      `docs/devenv/github-packages-pat-rotation.md`. The value was exposed in
      tool output during this work and must be treated as compromised.

      The radius is every copy of the token, not the three repos this
      retrospective happened to name: six repos consume the org feed and
      therefore need a copy (only three hold a repo-level Actions secret),
      org-level Actions secrets cannot be enumerated without org admin, and
      every machine that can reach Proton Pass vault `AI` → item
      `Github PAT` is a copy too. The token expires 2026-10-30 regardless.

      It was tracked as issue #57 under the convention in force at the time,
      which required every operational action to be a tracked issue; the
      runbook replaces that entry — the work changes no code in this repo,
      and the repos and machines outside it never learned about it from a
      tracker.

## Related

- #31, #47, #49, #50, #51, #52, #53, #54, #55, #56, #57
- studywise-api#883, #890, #891, #907, #908, #909
