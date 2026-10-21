# Consuming the Studywise .NET SDK from the CLI

How the `studywise` CLI restores and uses `YellowHouseStudio.Studywise.Sdk`,
the typed HTTP client published from `studywise-api`.

> **The canonical consumption guide lives in the API repo**:
> `studywise-api/docs/technical-standards/studywise-sdk-consumption.md`
> (install, `nuget.config`, CI source registration, package boundaries,
> publishing gates). This document only covers what is specific to *this*
> repo, plus the facts that are easy to get wrong when checking the feed.

Policy for *whether* the CLI uses the SDK lives in
[`docs/design/cli-design.md` § 7](../design/cli-design.md#7-sdk-adoption)
(Adopted 2026-10-18). The wiring itself is tracked by issue #31.

---

## Status: published, at 2.3.0

`YellowHouseStudio.Studywise.Sdk` **version 2.3.0 is on the org feed** and
restores successfully.

| | |
|---|---|
| Package id | `YellowHouseStudio.Studywise.Sdk` |
| Published version | **2.3.0** |
| Feed | `https://nuget.pkg.github.com/yellow-house-studio/index.json` |
| Visibility | **private** (org-scoped) — needs org access + a token with `read:packages` |
| Assembly | `Studywise.Contracts` (the assembly name differs from the package id) |
| Target framework | `net9.0` (consumable from this repo's `net10.0`) |

Verified on 2026-10-21:

```bash
dotnet add package YellowHouseStudio.Studywise.Sdk --version 2.3.0
# → Restored … (in 177 ms)
```

### Provenance

Published **2026-10-21T09:17:44Z** by the `Publish .NET SDK` job of a
`workflow_dispatch` run on the `feat/sdk-publish-single-workflow` branch
(studywise-api#884). That run's overall conclusion is `cancelled` — an
unrelated Docker publish job was killed at 09:33 — but the SDK publish job
itself was green and the package landed.

So the package exists, but it did **not** come from the canonical release
path: in that same run `Deploy to Production` was `skipped`. The
release-driven path (`release-deploy.yml` -> `publish-dotnet-sdk` ->
`sdk-pack-publish.yml`) has still never published anything. studywise-api#884
removed the tag-driven and manual publish paths precisely so there is only one
— but the first real publish came from a manual dispatch on the PR's own
branch, before that change was on `master`.

**Do not create an `sdk/v*` tag.** That trigger was removed in #884; the SDK
publishes as part of the release flow. A tag is a no-op.

### Version pinning

The SDK follows an **independent SemVer**, decoupled from the API release
version (`v6.x.y`). Pin it explicitly.

`2.3.0` is what is published. `origin/master` of studywise-api currently
reads `<Version>2.4.0</Version>` — bumped by the coupling gate when
`src/Studywise.Contracts/README.md` changed — and **2.4.0 has not been
published**. Pin `2.3.0`; a `2.4.0` reference will fail restore.

Read the csproj rather than trusting a number quoted in a doc or an issue:

```bash
git -C ../studywise-api show origin/master:src/Studywise.Contracts/Studywise.Contracts.csproj \
  | grep '<Version>'
```

---

## Checking the feed (and the trap)

**Do not probe the flatcontainer.** This is the mistake that makes the package
look unpublished when it is not:

```bash
# ✗ WRONG — 404 even though the package exists and is consumable
curl https://nuget.pkg.github.com/yellow-house-studio/v3/flatcontainer/yellowhousestudio.studywise.sdk/index.json
# → 404 page not found

# ✓ RIGHT — the /download/ endpoint is the feed's version index
curl -s -H "Authorization: Bearer $TOKEN" \
  https://nuget.pkg.github.com/yellow-house-studio/download/yellowhousestudio.studywise.sdk/index.json
# → {"versions":["2.3.0"]}
```

Anonymously the `download/` endpoint returns **401** (private package). The
flatcontainer 404s for this package either way — authenticated or not — so it
is not a signal of absence.

The unambiguous test is a real restore, which is what actually matters:

```bash
dotnet add package YellowHouseStudio.Studywise.Sdk --version 2.3.0
```

A machine with the feed and credentials configured restores in well under a
second. A 404 from the flatcontainer tells you nothing.

---

## Credentials in this repo

One org-wide PAT serves every consumer.

- **Source of truth** — Proton Pass, vault `AI`, item `Github PAT`. Fields:
  `PAT`, `Expires`, `Username`. The value must never land in this repo: not
  in a doc, not in a workflow file, not in a plan, not in a PR comment. Name
  the field and the source instead.
- **This machine** — `~/.nuget/NuGet/NuGet.Config` already declares the
  `github` source with its credentials, so local restore needs no setup.
- **CI** — GitHub Actions secrets are per repository; there is no org-level
  secret. **`studywise-cli` currently has zero repo secrets and zero
  environments**, so it needs its own copy. Use `NUGET_GITHUB_PACKAGES_TOKEN`
  for symmetry with `studywise-api`, and register the source before restore
  using the org-proven pattern from
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

### The repo-level `nuget.config`

Required once the SDK is referenced. It must map the package to the `github`
source, or NuGet fails with **NU1100** (no source mapped) rather than an
auth error:

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

The `<clear />` matters: without it the machine-level `github` source and its
credentials leak into the repo's effective config. With it, the repo config
fully determines the source list — which is why the source must be
re-declared above.

---

## Using it

```csharp
services.AddStudywiseClient(config.ApiBaseUrl, config.ApiKey);

var client = provider.GetRequiredService<StudywiseClient>();
var books  = provider.GetRequiredService<BooksClient>();
```

Per-area subclients, all sharing the same base address and API-key handler
pipeline: `AuthClient`, `BooksClient`, `EducationLevelsClient`,
`LiteratureClient`, `MaterialsClient`, `SchoolsClient`,
`SchoolSubjectsClient`, `SchoolYearsClient`.

The SDK sets `X-Studywise-Api-Key` itself via `ApiKeyAuthenticationHandler`,
so once the SDK is wired this repo's `ApiKeyDelegatingHandler` and
`ApiKeyTokenProvider` become redundant — as does `IStudywiseTransport` /
`HttpClientStudywiseTransport` (the SDK's typed client replaces them).

### Error handling

`AuthClient.VerifyAsync` — like every SDK method — throws
`HttpRequestException` on a non-success status code. Since .NET 5 that
exception carries the status code, so the `auth-verify` check **can**
distinguish a 401 (invalid or revoked key) from other failures:

```csharp
catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.Unauthorized)
{
    // invalid or revoked API key
}
```

Issue #31's plan assumed this information was lost and that it had to be
re-shaped out of the exception. It isn't.

---

## What the SDK does not cover

The SDK ships no `CountriesClient`, no `HomeworkClient`, and no
words/vocabulary client. CLI issues #33 (countries list) and #34–#36
(homework vocabulary creation) therefore have **no SDK endpoint to sit on
yet** — those need API-side SDK work first, not CLI work.

---

## Follow-ups this document does not cover

Deliberately left to the issue #31 wiring PR, where the SDK actually becomes
a dependency:

- `scripts/verify.sh` asserts "the CLI repo has no private NuGet feeds" in
  two places — the header block, and the `NU1301` / 401 error handler. Both
  go stale the moment the SDK is referenced, and the 401 handler will then
  give the wrong message for a real credential failure.
- The `nuget.config` above needs adding to the repo, and the
  `NUGET_GITHUB_PACKAGES_TOKEN` secret needs creating.
- `Directory.Packages.props` (Central Package Management) is referenced by
  `docs/design/cli-design.md` § 7 but does not exist in this repo yet. Either
  introduce it or amend § 7.
