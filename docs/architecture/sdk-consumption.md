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
- **This machine** — the user-level `~/.nuget/NuGet/NuGet.Config` declares the
  `github` source and carries its credentials, so local restore needs no
  setup. The credential belongs there as a `%NUGET_GITHUB_TOKEN%` reference
  to a mode-600 env file, never as a literal `ClearTextPassword` — a literal
  there is what leaked on 2026-10-21. The rotation procedure, including the
  env-file form, is in
  [`docs/devenv/github-packages-pat-rotation.md`](../devenv/github-packages-pat-rotation.md).
- **CI** — GitHub Actions secrets are per repository; there is no org-level
  secret for the feed. This repo holds its own copy as
  `NUGET_GITHUB_PACKAGES_TOKEN` (set 2026-10-21), and registers the source
  before restore:

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

  Every repo that consumes the feed holds its own copy of the token —
  there is no org-level secret for the feed, and those copies are the
  credential's whole radius. The rotation procedure, and the list of
  copies as measured on 2026-10-22, are in
  [`docs/devenv/github-packages-pat-rotation.md`](../devenv/github-packages-pat-rotation.md).

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
so this repo no longer declares the header, the credential, or a transport
abstraction of its own — `ApiKeyDelegatingHandler`, `ApiKeyTokenProvider`,
`ITokenProvider`, `IStudywiseTransport` and `HttpClientStudywiseTransport`
are all gone.

### Redirects and the API key

`AddStudywiseClient` exposes no hook for the primary message handler, and
`HttpClient` follows redirects by default. Because the SDK attaches the API
key to every request, a 3xx from the API would forward the credential to the
redirect target host.

This repo therefore re-registers the SDK clients it uses on top of the SDK's
own registration and disables redirect following:

```csharp
services.AddStudywiseClient(config.ApiBaseUrl, config.ApiKey);
services.AddHttpClient<AuthClient>().UseStudywiseDefaults(config);
```

`StudywiseHttpClientExtensions.UseStudywiseDefaults` also applies the CLI's
User-Agent. Both registrations configure the same named `HttpClient`, so the
SDK's base address, timeout and API-key handler still apply.

The durable fix belongs in the SDK — tracked as
[studywise-api#890](https://github.com/yellow-house-studio/studywise-api/issues/890).
Until it lands, the CLI-side configuration is covered by an integration test
that asserts a redirect is not followed.

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

The SDK also ships no health endpoint, so the `connection` diagnostic still
uses a plain `HttpClient` to probe the API. Tracked as
[studywise-api#891](https://github.com/yellow-house-studio/studywise-api/issues/891).
Separately, that probe currently requests `/health`, which the API does not
map — see [studywise-cli#48](https://github.com/yellow-house-studio/studywise-cli/issues/48).

---

## Prerequisites, and where they landed

| Prerequisite | Where it lives |
|---|---|
| `Directory.Packages.props` (Central Package Management) | repo root — the SDK version is pinned there |
| `nuget.config` with package source mapping | repo root |
| `NUGET_GITHUB_PACKAGES_TOKEN` repository secret | this repo, set 2026-10-21 — one copy per consuming repo |
| CI source registration | `ci-fast.yml`, `ci-full.yml`, `ci-production.yml` |
| `verify.sh` NuGet-auth wording | `scripts/verify.sh` — the header and the `NU1301` / 401 handler now describe the private feed |
