# Releasing (maintainers)

Releases are fully automated by [semantic-release](https://semantic-release.gitbook.io/) — nobody
edits version numbers or tags by hand.

## How a release happens

1. Commits land on `main` (house style: `emoji type: description`, e.g. `✨ feat: …` — the
   [release.config.mjs](../release.config.mjs) parser accepts the gitmoji prefix).
2. [release.yml](../.github/workflows/release.yml) runs the full test matrix
   (ubuntu/windows × net8.0/net10.0); only if green does the release job start, inside the
   `nuget` GitHub environment.
3. semantic-release analyzes the commits since the last `v*` tag:

   | Commits since last tag contain | Next version |
   |-------------------------------|--------------|
   | `✨ feat!:` or a `BREAKING CHANGE:` footer | major |
   | `✨ feat:` | minor |
   | `🐛 fix:` / `⚡ perf:` | patch |
   | only `docs`, `chore`, `ci`, `test`, `refactor`, `style` | **no release** |

4. On a release it updates [CHANGELOG.md](../CHANGELOG.md) and `Directory.Build.props`, packs
   every project under `src/`, publishes the `.nupkg`/`.snupkg` to NuGet, tags `vX.Y.Z`, creates
   the GitHub release and pushes a `🔧 chore: release vX.Y.Z [skip ci]` commit.

The `preview` branch publishes pre-releases (`X.Y.Z-preview.N`) with the same flow.

## After a release

Local clones are one commit behind (the release commit). Sync before continuing work:

```bash
git pull --rebase origin main
```

## CI safeguards

- [ci.yml](../.github/workflows/ci.yml) runs the same matrix on every branch/PR plus a pack
  smoke and a **Native AOT publish smoke** ([test/eQuantic.Validation.AotSmoke](../test/eQuantic.Validation.AotSmoke/Program.cs))
  that publishes with `PublishAot=true` and executes the native binary.
- Generator emission is tested in-memory under a comma-decimal culture (pt-BR) to keep generated
  code culture-proof.
