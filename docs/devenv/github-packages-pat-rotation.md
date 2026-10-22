# Rotating the GitHub Packages PAT

The org-wide GitHub Packages PAT that feeds `YellowHouseStudio.Studywise.Sdk`
was printed into tool output during the SDK adoption work (2026-10-21). Any
copy of it must be treated as compromised. This is the rotation procedure:
ordered, with the verification at the end.

It replaces [studywise-cli#57](https://github.com/yellow-house-studio/studywise-cli/issues/57),
which was the wrong shape for the work — a tracker entry in a code repo for an
operator chore that changes no code here. The reasoning is in
[the retrospective](../../retrospective/feature-sdk-adoption.md).

**Generic version:** the org-wide pattern — why the token lives in one file,
why the feed needs a token at all, and the full rotation flow — is in
[`swe-skills-and-docs/docs/github-packages-auth.md`](https://github.com/fam-hulten/swe-skills-and-docs/blob/main/docs/github-packages-auth.md)
§ 2 and § 6. This runbook is the CLI-specific instance: what the radius of
this credential actually is, how to enumerate it, and how to prove the
rotation was complete.

---

## The radius

The token is **one item**: Proton Pass, vault `AI`, item `Github PAT`,
field `PAT`. Everything else is a copy of it.

So the radius is not "the repos that use the feed" — it is:

> **every consumer that pulls this token from this Proton Pass item, every
> machine that can reach that item, and every place the value has already
> landed.**

Three axes, none of them a fixed list:

| Axis | What it covers |
|---|---|
| **Consumers** | Every repo, service and project that restores from `https://nuget.pkg.github.com/yellow-house-studio/index.json`. Each one needs *a* token — from a repo secret, an org secret, or a developer's local setup. |
| **Copies** | Every store the value has been written to: Actions secrets, user-level `NuGet.Config` files, env files, Docker/BuildKit secret files, shell history, CI logs. |
| **Machines** | Every machine whose user can open Proton Pass vault `AI` → item `Github PAT`, and every machine whose NuGet config holds a copy. This is the axis people forget: the credential is one line in a password manager, and every developer machine that syncs it is a copy of a credential with `repo` scope. |

### The amplifier: `repo` scope

The token in use carries `read:packages`, `read:project` and **`repo`**.
`repo` is not a packages scope — it is full access to every private
repository in the org, the same access a personal access token has. Every
copy in the table above is therefore an **org-wide credential**, not a
package-feed credential, and each CI runner that holds it inherits that
reach for the length of the job.

This is why narrowing the scope is part of the rotation and not a follow-up:
the radius of the next leak is proportional to the scope of the token. If
the post-push read-back verification that needs `repo` moves to the
workflow's own `GITHUB_TOKEN`, this token becomes a fine-grained token with
**Packages: Read** on the org, and every copy above shrinks to package-feed
access.

### Measure the radius — do not trust a list

A list of copies is wrong the moment the org changes: the feed has more
consumers than the three repos that hold a CI secret, org-level secrets are
invisible without `admin:org`, and machines are unbounded. Enumerate.

```bash
# 1. Which repos consume the feed at all?
gh search code "nuget.pkg.github.com/yellow-house-studio" \
  --owner yellow-house-studio --json repository,path --limit 50 \
  | python3 -c "import json,sys; print(sorted({i['repository']['nameWithOwner'] for i in json.load(sys.stdin)}))"

# 2. Which of them hold a repo-level Actions secret for it?
for r in <every repo from step 1>; do
  printf '%-28s ' "$r"; gh secret list --repo "yellow-house-studio/$r" | awk '{printf "%s ", $1}'; echo
done

# 3. Org-level secrets — only an org admin can see these.
gh secret list --org yellow-house-studio      # 403 without admin:org
```

Step 3 is the one that cannot be closed from this repo. If org-level
Actions secrets exist, only an org admin can enumerate and rotate them —
that is a handoff, and the rotation is not provably complete without it.

### Snapshot (measured 2026-10-22)

| Surface | Count | Which |
|---|---|---|
| Repos referencing the feed | 6 | `studywise-cli`, `studywise-api`, `yhs-core-services`, `klara`, `image-communication`, `spark-progress-backend` |
| Repos with a repo-level Actions secret for the token | 3 | `studywise-cli` (`NUGET_GITHUB_PACKAGES_TOKEN`), `studywise-api` (`NUGET_GITHUB_PACKAGES_TOKEN`), `yhs-core-services` (`NUGET_GITHUB_TOKEN`) |
| Repos consuming the feed without a repo secret | 3 | `klara`, `image-communication`, `spark-progress-backend` — they get the token from a different path (local scripts, another secret, or an org secret) |
| Org-level Actions secrets | unknown | needs `admin:org` |
| Machines | unbounded | every machine with Proton Pass access to vault `AI` |
| Transcripts / logs | ≥ 1 | the 2026-10-21 agent session where the value was printed |

Re-derive this table before you rely on it. The commands above are the
source; this table is a snapshot.

---

## The steps

### 0. Enumerate the copies

Run the measurement commands above and write the result down — repos with a
CI copy, repos consuming the feed by another route, org-level secrets (via
an admin), and the machines you have access to. You cannot complete step 6
without this list.

### 1. Create the new token (operator — GitHub UI)

<https://github.com/settings/tokens> → **Generate new token (classic)**.

- Scopes: `read:packages`, `read:project`, `repo` — or the narrower set
  above, if you are shrinking the scope in the same pass.
- Expiration: ≤ 90 days.

Do the scope change in the same pass as the rotation, not after it: a
rotation is the one moment every copy is being rewritten anyway, and the
narrower token shrinks the radius of the next incident for free.

### 2. Update the Proton Pass item (operator — Proton Pass UI)

Vault `AI` → item `Github PAT` → field `PAT` → new value. Field `Expires` →
the new expiry date.

Agents do not write to Proton Pass — the `proton-pass-cli` skill is
read-only by contract ("when not to use: creating, updating, trashing or
untrashing Proton Pass items"). This step is manual.

### 3. Update this machine

The local copy today is a **literal** PAT in `~/.nuget/NuGet/NuGet.Config`:

```xml
<add key="ClearTextPassword" value="ghp_..." />
```

That literal is what leaked — the redaction pass missed the attribute name.
Rotate into the form that cannot leak the same way: the token lives in a
mode-600 env file, and the config references an environment variable that
NuGet expands.

```bash
# 3a. Write the env file (mode 600). The value comes from Proton Pass;
#     it must not be echoed, and it must not be typed into a shell that
#     records history.
install -m 600 /dev/null "$HOME/.config/yhs-nuget.env.tmp"
pass-cli item view --vault-name AI --item-title "Github PAT" --output json \
  | python3 -c 'import json,sys; d=json.load(sys.stdin); print("export NUGET_GITHUB_TOKEN=\"%s\"" % next(f["content"]["Hidden"] for f in d["item"]["content"]["extra_fields"] if f["field_name"]=="PAT"))' \
  > "$HOME/.config/yhs-nuget.env.tmp"
mv "$HOME/.config/yhs-nuget.env.tmp" "$HOME/.config/yhs-nuget.env"
chmod 600 "$HOME/.config/yhs-nuget.env"

# 3b. Point the user-level NuGet.Config at the env var instead of a literal.
#     The env-var name is NUGET_GITHUB_TOKEN — never GITHUB_TOKEN, which
#     collides with the gh CLI's auth precedence and shadows the user's
#     ~/.config/gh/hosts.yml.
```

The command above writes the value into the file and the file only — the
token never reaches stdout, so it never reaches a tool result.

Every other machine in the radius needs step 3 as well. If a machine holds a
literal `ClearTextPassword` for the feed, replace it the same way; if it
holds the env file under a different name, keep that name and update the
file.

> **Known gap.** The CLI's `scripts/setup-env.sh` does not source the env
> file, so `dotnet restore` needs the variable exported:
> `source ~/.config/yhs-nuget.env` before the restore, or add it to your
> shell rc. Until that is fixed, a fresh shell without the variable fails
> restore with NU1301 — a confusing error for a token that is present and
> correct.

### 4. Update every CI copy

Pipe the value straight from Proton Pass into each secret store. It must
not pass through a variable on the command line, and it must not be printed.

```bash
set_pat() {  # $1 = repo, $2 = secret name
  pass-cli item view --vault-name AI --item-title "Github PAT" --output json \
    | python3 -c 'import json,sys; d=json.load(sys.stdin); print(next(f["content"]["Hidden"] for f in d["item"]["content"]["extra_fields"] if f["field_name"]=="PAT"), end="")' \
    | gh secret set "$2" --repo "$1"
}

set_pat yellow-house-studio/studywise-cli        NUGET_GITHUB_PACKAGES_TOKEN
set_pat yellow-house-studio/studywise-api        NUGET_GITHUB_PACKAGES_TOKEN
set_pat yellow-house-studio/yhs-core-services    NUGET_GITHUB_TOKEN
```

Those are the three repo-level secrets that existed at the snapshot. Step 0
is what tells you whether that list is still true, and it is the list to
work from — not this one.

Org-level secrets are a separate handoff: an org admin rotates those in
the org settings.

Confirm the writes landed by timestamp, not by value:

```bash
gh secret list --repo yellow-house-studio/studywise-cli
gh secret list --repo yellow-house-studio/studywise-api
gh secret list --repo yellow-house-studio/yhs-core-services
```

### 5. Prove every copy, before revoking anything

A token that was never exercised is not a token that works.

```bash
# Local: a real restore, not a probe.
source ~/.config/yhs-nuget.env
dotnet restore --force
```

```bash
# The feed's version index, authenticated. The flatcontainer 404s for
# private packages whether or not they exist — it is not a signal.
curl -s -H "Authorization: Bearer $NUGET_GITHUB_TOKEN" \
  https://nuget.pkg.github.com/yellow-house-studio/download/yellowhousestudio.studywise.sdk/index.json
# → {"versions":["2.3.0", ...]}
```

Each CI copy is exercised by the next CI run in its repo — the secret is
read at restore time, so a green restore is the proof. Do this for every
repo in the radius that has a CI copy, not just this one.

### 6. Revoke the old token (operator — GitHub UI)

Only after step 5 is green everywhere: <https://github.com/settings/tokens>
→ the old token → **Delete**.

Revoking last means nothing is broken mid-flight. If the old token is
already expired, the step is a no-op — delete it anyway so it does not sit
in the settings page looking live.

---

## Rules that make this safe

- **Never print the token.** Extract it into a file or pipe it into the
  writer. A value that reaches stdout reaches the tool-result stream and
  the session transcript — that is the whole failure this runbook exists
  for.
- **Never paste it into a tracked file, a workflow, a doc, a PR body or a
  comment.** Name the field and the source instead: "Proton Pass, vault `AI`,
  item `Github PAT`, field `PAT`".
- **Never `cat` a credential-bearing file.** `cat ~/.nuget/NuGet/NuGet.Config`
  prints the token when the config holds a literal — that is exactly how the
  2026-10-21 exposure happened. Read the value into a variable and use it.
- **Enumerate the radius; never work from a remembered list.** The set of
  copies is bigger than the set you know about, and it changes.
- **Verify by doing the real operation.** A 200 from a probe endpoint, a
  green `gh auth status`, or an exit code from a convenience script is a
  proxy. The real operation is a restore that pulls the package.
- **Revoke last.** A rotation that revokes first breaks every copy that has
  not been updated yet.

---

## Related

- [studywise-cli#57](https://github.com/yellow-house-studio/studywise-cli/issues/57)
  — the tracker entry this runbook replaces.
- [`docs/architecture/sdk-consumption.md`](../architecture/sdk-consumption.md)
  § "Credentials in this repo" — where the credential is consumed.
- [`docs/retrospective/feature-sdk-adoption.md`](../retrospective/feature-sdk-adoption.md)
  — the exposure and the lessons it produced.
- [`swe-skills-and-docs/docs/github-packages-auth.md`](https://github.com/fam-hulten/swe-skills-and-docs/blob/main/docs/github-packages-auth.md)
  § 6 — the org-wide rotation pattern.
