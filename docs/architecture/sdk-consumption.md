# Consuming the Studywise .NET SDK

How the `studywise` CLI restores and consumes `YellowHouseStudio.Studywise.Sdk`
— the typed HTTP client published from `studywise-api`.

Policy for *whether* the CLI uses the SDK lives in
[`docs/design/cli-design.md` § 7](../design/cli-design.md#7-sdk-adoption)
(Adopted 2026-10-18). This document is the mechanics: feed, credentials,
version pinning, and the two restore failure modes that are easy to misread.

---

## Status: the package is not published yet

**As of 2026-10-21, no version of `YellowHouseStudio.Studywise.Sdk` exists on
any feed.** The SDK source ships in `studywise-api` at
`src/Studywise.Contracts/`, but the publish step had never actually run — the
job reported green while silently skipping the push. That was fixed upstream in
`studywise-api#884` (merged 2026-10-21).

Consequences for anyone following this document:

- **`dotnet add package YellowHouseStudio.Studywise.Sdk` will fail today**
  with a "package not found" error. That is expected, not a misconfiguration.
- **Do not create an `sdk/v*` tag.** That trigger was deliberately removed in
  `studywise-api#884`; the SDK now publishes as part of the normal release
  flow (`release-deploy.yml` → `publish-dotnet-sdk` → `sdk-pack-publish.yml`).
  A tag would be a no-op.

The publish is gated on: the SDK source changed in the release (or the operator
forced it), the production deploy succeeded, and `deploy-verify` was green.

### Version pinning

The SDK follows an **independent SemVer**, decoupled from the API release
version (`v6.x.y` on the API repo). Pin the SDK version explicitly; never
assume it tracks the API release.

The version that will be published is whatever `<Version>` reads in
`studywise-api/src/Studywise.Contracts/Studywise.Contracts.csproj` at release
time. At the time of writing that is **2.4.0** — but read it from the csproj
rather than trusting a number quoted in a doc or an issue, because the coupling
gate bumps it whenever the SDK source changes.

```bash
# The authoritative source for the version to pin:
git -C ../studywise-api show origin/master:src/Studywise.Contracts/Studywise.Contracts.csproj \
  | grep '<Version>'
```

---

## The package

| | |
|---|---|
| Package id | `YellowHouseStudio.Studywise.Sdk` |
| Feed | `https://nuget.pkg.github.com/yellow-house-studio/index.json` |
| Visibility | **private** (org-scoped) — needs org access + a PAT with `read:packages` |
| Target framework | `net9.0` (consumable from this repo's `net10.0`) |
| API base address | `https://api.studywise.se` |
| Upstream docs | `studywise-api/src/Studywise.Contracts/README.md`, `studywise-api/docs/technical-standards/sdk-surface.md` |

Note the package name: `YellowHouseStudio.Studywise.*`. There is no
`YellowHouseStudio.Studywise.Contracts` package — that name was superseded
before the first publish, and any doc still naming it is stale.

---

## Credentials

One org-wide PAT serves every consumer. It lives in three places depending on
who is consuming.

### Source of truth — Proton Pass

Vault `AI`, item **`Github PAT`**. Fields: `PAT` (the hidden value), `Expires`,
`Username`. Scopes: `read:packages`, `read:project`, `repo`.

Check the `Expires` field there before relying on it — the PAT is on a
rotation cycle, and an expired PAT surfaces as an opaque `NU1301` during
restore, not as "your token expired".

> The PAT value never belongs in this repository. Not in a doc, not in a
> workflow file, not in a plan or retrospective, not in a PR comment. A
> truncated value is still a leak — the secret scanner matches on entropy, not
> on whether the string is complete. Name the field and the source; let the
> consumer pull the value.

### This machine

`~/.nuget/NuGet/NuGet.Config` already declares the `github` source together
with its credentials, and `~/.config/klara-nuget.env` holds the environment
variable form (`NUGET_GITHUB_TOKEN`) plus a `NUGET_PASS_ITEM_ID` pointer back
to Proton Pass.

**Local restore therefore needs no extra setup** — `dotnet add package` just
works once the package is actually published.

### CI

There is **no org-level Actions secret** to lean on (creating one requires org
admin). Each repo carries its own copy of the same value under whatever name
it already uses:

| Repo | Secret name |
|---|---|
| `studywise-api` | `NUGET_GITHUB_PACKAGES_TOKEN` |
| `yhs-core-services` | `NUGET_GITHUB_TOKEN` |
| `studywise-cli` | **not created yet** — this repo currently has zero repo secrets and zero environments. Use `NUGET_GITHUB_PACKAGES_TOKEN` for symmetry with `studywise-api`. |

`read:packages` is enough to *consume*. It is not enough to *publish* — which
is why publishing from `studywise-api` uses the workflow's own `GITHUB_TOKEN`
(`packages: write`) and the PAT only for the post-push read-back verification.

---

## Local restore

```bash
dotnet add src/Studywise.Cli/Studywise.Cli.csproj package \
  YellowHouseStudio.Studywise.Sdk --version <pin>
```

No `--configfile` override and no `nuget.config` change are needed locally:
the user-level config already provides the `github` source and its
credentials, and this repo has no repo-level `nuget.config` yet to shadow it.

---

## CI restore

CI has no user-level NuGet config, so the source has to be registered before
the restore step. This is the org-proven pattern, copied from
`studywise-api/.github/workflows/deploy-verify.yml`:

```yaml
- name: Add GitHub Packages NuGet source
  run: |
    dotnet nuget remove source github --configfile nuget.config || true
    dotnet nuget add source \
      "https://nuget.pkg.github.com/yellow-house-studio/index.json" \
      --name github \
      --username "${{ github.repository_owner }}" \
      --password "${{ secrets.NUGET_GITHUB_PACKAGES_TOKEN }}" \
      --store-password-in-clear-text
```

The `remove --configfile nuget.config` line is not redundant: it drops the
credential-less `github` entry from the repo-level config so the credentialed
one from the user-level config is the single `github` source NuGet resolves.

### The repo-level `nuget.config`

Required once the SDK is referenced, and it must map the package to the
`github` source:

```xml
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" protocolVersion="3" />
    <add key="github" value="https://nuget.pkg.github.com/yellow-house-studio/index.json" />
  </packageSources>
  <packageSourceMapping>
    <packageSource key="nuget.org">
      <package pattern="*" />
    </packageSource>
    <packageSource key="github">
      <package pattern="YellowHouseStudio.Studywise.Sdk" />
    </packageSource>
  </packageSourceMapping>
</configuration>
```

This mirrors `studywise-api/nuget.config`.

---

## Two restore failure modes that mislead

### NU1100, not 401 — a missing source mapping

If a `nuget.config` has a `<packageSourceMapping>` that does not map
`YellowHouseStudio.Studywise.Sdk` to the `github` source, NuGet fails with
**NU1100** ("no source is mapped to the package"), *not* an authentication
error. It reads like "the package doesn't exist" when the real problem is the
mapping. Check the mapping before concluding the package was never published.

### `<clear />` wipes the user-level source

A `<clear />` inside `<packageSources>` discards the `github` source — and its
credentials — that `~/.nuget/NuGet/NuGet.Config` provides. The repo-level
config then has to re-declare `github` itself, and CI has to re-add the
credentials. `studywise-api`'s `nuget.config` does exactly this; keep the
`<clear />` and re-declare both sources.

---

## Usage

```csharp
services.AddStudywiseClient("https://api.studywise.se", apiKey);

var client = provider.GetRequiredService<StudywiseClient>();
var books  = provider.GetRequiredService<BooksClient>();
```

Per-area subclients, all sharing the same base address and API-key handler
pipeline: `AuthClient`, `EducationLevelsClient`, `SchoolYearsClient`,
`SchoolSubjectsClient`, `MaterialsClient`, `LiteratureClient`, `BooksClient`,
`SchoolsClient`.

The SDK sets the `X-Studywise-Api-Key` header itself via
`ApiKeyAuthenticationHandler`, so once the SDK is wired the CLI's own
`ApiKeyDelegatingHandler` and `ApiKeyTokenProvider` become redundant. See
issue #31 for the full swap.

### Error handling

`AuthClient.VerifyAsync` (and every other SDK method) throws
`HttpRequestException` on a non-success status code. Since .NET 5 that
exception carries the status code in `HttpRequestException.StatusCode`, so the
CLI's `auth-verify` check **can** distinguish a 401 (invalid or revoked key)
from other failures without the SDK leaking extra information:

```csharp
catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.Unauthorized)
{
    // invalid or revoked API key
}
```

---

## What the SDK does not cover

The SDK currently ships no `CountriesClient`, no `HomeworkClient`, and no
words/vocabulary client. CLI issues #33 (countries list) and #34–#36 (homework
vocabulary creation) therefore have **no SDK endpoint to sit on yet** — those
need API-side SDK work first, not CLI work.

---

## Follow-ups this document does not cover

Deliberately left to the issue #31 wiring PR, not to this document:

- `scripts/verify.sh` asserts in two places that "the CLI repo has no private
  NuGet feeds" and refers to "the public `Studywise.*` packages" (the header
  block, and the `NU1301` / 401 error handler). Both become wrong the moment
  the SDK lands, and the 401 handler in particular will then be the wrong
  message for a real credential failure. Update them in #31.
- Issue #31's draft `nuget.config` omits `<clear />`; the version above
  includes it, matching `studywise-api`.
- Issue #31 pins the SDK at `1.0.0`. The csproj says `2.4.0`. Correct this
  when the wiring PR is written.
