# release-notes

A .NET tool that turns a GitHub repository's closed issues and pull requests into release notes, grouped by
version labels (`v1.2.3`) and category labels (`--label bug=Bug Fixes`). Its commands are `generate`,
`apply-labels`, `find-previous`, `current-version` and `create-release`. It is also published as a distroless
container (`ghcr.io/nullean/release-notes`) and a GitHub Action (`action.yml`) that runs it.

## Layout

| Path | What |
|---|---|
| `src/release-notes` | the tool (net8.0 and net10.0), a `Nullean.Argh` CLI; `ReleaseNotesCommands.cs` holds the commands |
| `src/release-notes/GitHubReleaseClient.cs` | the GitHub writes (releases, labels), source-generated JSON instead of Octokit |
| `build/scripts` | the F# build ([Nullean.Make.Fs](https://www.nuget.org/packages/Nullean.Make.Fs), net11.0) the `mise` tasks and CI call |
| `build/keys` | the strong-name key the tool's own assembly is signed with |
| `action.yml` | the GitHub Action: runs the `edge` container image |

## Commands

mise installs the SDK pinned in `global.json` (plus the net10.0 runtime nupkg-validator runs on). Run
everything through it:

```bash
mise run build    # build the solution
mise run pack     # build, pack, validate the packages, report public API changes
mise run ci       # what CI's build job runs: pack (there are no tests), then the curb check
mise run format   # format the repository with curb
mise run check    # formatting drift (curb) and workflow security checks (zizmor); CI runs the curb half
mise run clean    # delete build output
mise run release  # show the release draft; `mise run release publish` releases
```

`mise tasks ls` lists them all. Packages and API reports go to `build/output`. `./build.sh <target>` runs the same
build targets directly; CI calls it step by step, including `publishcontainers`, which has no `mise` task.

## Releasing

release-drafter keeps a draft release up to date from the labels of merged pull requests. **Publishing the draft**
releases: it creates the tag, and `ci.yml` runs on it to build every RID's native-AOT package, push
`ghcr.io/nullean/release-notes` (tagged `latest` and the version) and push the packages to nuget.org. `mise run release`
shows the draft. The [releasing](.claude/skills/releasing/SKILL.md) and
[breaking-changes](.claude/skills/breaking-changes/SKILL.md) skills have the details. Every pull request carries exactly
one release label. This repository's own releases do not run the tool: release-drafter writes their notes.

## Packaging

- The tool ships as native-AOT packages, one per RID (`linux-x64`, `linux-arm64`, `win-x64`, `win-arm64`,
  `osx-arm64`), a root package that points at them, and a framework-dependent `release-notes.any` fallback.
- AOT only compiles for the machine it runs on, so a local `pack` keeps just the root and `any` packages. CI's
  `aot-pack` matrix builds each RID on a matching runner and the build job merges them before validating.
- `dotnet pack` puts the unsigned `obj/` assembly into the `any` package; `generatePackages` swaps in the signed
  `bin/` copy (`fixAnyPackageSigning`). `Paths.ManagedTargetFrameworks` must mirror the csproj's
  `TargetFrameworks`.

## Rules

- The CLI surface (the commands and their flags) is the contract `action.yml`, the container and other repositories'
  builds rely on. Removing or renaming any of it is a breaking change.
- Octokit's reflection-based JSON is unreliable under native AOT (see the remarks on `GitHubReleaseClient`). Request
  bodies go through `GitHubJsonContext`, and writes are idempotent upserts rather than check-then-create.
- Build targets are Make.Fs `target { }` blocks in `build/scripts/Program.fs` whose commands are Proc.Fs `sh $"…"`
  lines; values come from `text` inside `shell { }` (`Versions.fs`). Do not call the C# `ProcNet` API from the build.
  CI calls targets by name (`./build.sh generatepackages -s true`): keep multi-word case names as one word.
