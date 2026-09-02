<p>
<img align="right" src="nuget-icon.png">  

# release-notes
</p>

Generate release notes for a release based on github labels and closed issues and PR's

## Installation

Distributed as a .NET tool so install using the following

```
dotnet tool install release-notes
```

On Linux, Windows and macOS/arm64, this resolves to a self-contained native-AOT executable — no
shared .NET runtime required, and no first-run JIT warmup. Everywhere else, it falls back to a
framework-dependent build (requires the .NET runtime the tool targets to already be installed).

## GitHub Action

```yaml
- uses: nullean/release-notes@main
  with:
    command: generate
    owner: nullean
    repo: release-notes
    args: --version 1.0.0
```

Runs `release-notes` from a pre-built, distroless container (`ghcr.io/nullean/release-notes`) — no .NET
SDK install needed in the workflow. `command` is one of `generate`, `apply-labels`, `find-previous`,
`current-version`, or `create-release`; extra flags pass through verbatim via `args`. Linux runners only
(`ubuntu-latest` and similar) — container actions can't run on Windows or macOS runners.

## Container image

`ghcr.io/nullean/release-notes` also works as a general-purpose container, outside GitHub Actions —
GitLab CI, a local machine without the .NET SDK, anywhere `docker run` works:

```sh
docker run --rm ghcr.io/nullean/release-notes:edge generate nullean release-notes --version 1.0.0 --token "$GITHUB_TOKEN"
```

Distroless: native-AOT, chiseled `runtime-deps` base, no shell, runs as a non-root user. Tags follow
`release-notes`'s own releases — `edge` tracks the latest commit on `master`, `latest` and a semver tag
(e.g. `0.10.0`) follow tagged releases.

## Run

```bat
dotnet release-notes <command> <owner> <repository-name> --version <string> [options]
```

You can omit `dotnet` if you install this as a global tool.

> [!IMPORTANT]
> Starting from `1.0.0`, every invocation requires an explicit command name (`generate`,
> `apply-labels`, `find-previous`, `current-version` or `create-release`) — there is no longer a bare
> `release-notes <owner> <repo> ...` shorthand for `generate`. `--label` also changes from two tokens
> (`--label <label> <description>`) to one combined token (`--label <label>=<description>`), and every
> other flag moves from a single word to kebab-case (e.g. `--oldversion` → `--old-version`). This is a
> breaking change from earlier `0.x` releases, a side effect of moving off `Argu` (which no longer
> worked once this tool was AOT-compiled) onto [`Nullean.Argh`](https://github.com/nullean/argh).

```bat
Usage: release-notes <namespace|command> [options]

Commands:
  apply-labels     Creates the version and backport labels for the next release.
  create-release   Makes sure the tag exists as a release on GitHub, and
                   introduces new version labels for the next major/minor/patch.
  current-version  Given a search query, finds the current and the next versions
                   and prints them on separate lines.
  find-previous    Finds and prints the previous release for the given version.
  generate         Generates release notes for version from closed GitHub issues
                   and PRs, printed to standard out and optionally written to a
                   file.
```

Every command takes the same `<owner> <repository-name>` positionals and shared options:

```bat
Arguments:
  <owner>            GitHub repository owner.
  <repository-name>  GitHub repository name.

Options:
  --version <string>                [required] Version that is being released.
  --token <string>                  The GitHub token to use. If the issue list is long this may be necessary; defaults to anonymous.
  --old-version <string>            The previous version to generate release notes since. Optional; the tool will find the previous release.
  --release-tag-format <string>     The release tag format. VERSION is replaced by the actual version. [default: VERSION]
  --release-label-format <string>   The release label format. VERSION is replaced by the actual version. [default: vVERSION]
  --backport-label-format <string>  The backport label format, e.g. "Backport BRANCH". BRANCH is calculated from the version.
  --format <enum>                   The format in which to print the results: Markdown or AsciiDoc. [default: markdown]
  --uncategorized-header <string>   The header to use in the markdown for uncategorized issues/PRs.
  --label <values>                  [repeatable] Map a GitHub label to a categorization heading, formatted as <label>=<description>. May be given
                                     more than once. Defaults to bug=Bug Fixes, enhancement=New Features, documentation=Documentation Improvements.
  --output <string>                 Write the release notes to a file as well as standard out. VERSION is replaced by the actual version.
```

#### Examples

Generate markdown release notes to standard out (and print to a file with `--output`):

```bat
dotnet release-notes generate nullean release-notes --version 1.0.0
```

Find the previous release for a version:

```bat
dotnet release-notes find-previous nullean release-notes --version 1.0.0
```

Given a search query (`M.N`, `M.x`, or `master`/`main`), print the current and next version on separate lines:

```bat
dotnet release-notes current-version nullean release-notes --version 1.0.0 --query master
```

Create the version and backport labels for the next release:

```bat
dotnet release-notes apply-labels nullean release-notes --version 1.0.0 --backport-label-format "Backport BRANCH"
```

Make sure the tag exists as a release on GitHub, using one or more files for the release body, and
introduce new version labels for the next major/minor/patch:

```bat
dotnet release-notes create-release nullean release-notes --version 1.0.0 --body notes.md --body breaking-changes.md --token $GITHUB_TOKEN
```

Map GitHub labels to custom categorization headings (replaces the default bug/enhancement/documentation set):

```bat
dotnet release-notes generate nullean release-notes --version 1.0.0 --label bug=Bug Fixes --label enhancement=New Features
```
