<p align="center">
  <picture>
    <source media="(prefers-color-scheme: dark)" srcset="https://raw.githubusercontent.com/nullean/nullean.github.io/main/assets/nullean-lockup-544x192.png">
    <img src="https://raw.githubusercontent.com/nullean/nullean.github.io/main/assets/nullean-lockup-light-544x192.png" width="272" alt="nullean">
  </picture>
</p>

<h1 align="center">release-notes</h1>

<p align="center">Write release notes for a GitHub repository from the issues and pull requests labelled with the version you are releasing, grouped by their other labels.</p>

<p align="center">
  <a href="https://www.nuget.org/packages/release-notes"><img src="https://img.shields.io/nuget/v/release-notes?style=flat-square" alt="NuGet"></a>
  <a href="https://github.com/nullean/release-notes/actions/workflows/ci.yml"><img src="https://img.shields.io/github/actions/workflow/status/nullean/release-notes/ci.yml?branch=main&style=flat-square&label=ci" alt="CI"></a>
</p>

The release notes for elastic/apm-agent-dotnet 1.11.0, from the three pull requests labelled `v1.11.0`. The previous
release is found on its own:

```console
$ release-notes generate elastic apm-agent-dotnet --version 1.11.0 --uncategorized-header "Other changes"
https://github.com/elastic/apm-agent-dotnet/compare/1.10.0...1.11.0

## New Features

- #1239 Add Cosmos DB support  (issue: #1154)
- #1310 Implement config to ignore the sampled flag in traceparent

## Other changes

- #1302 Prefer W3C traceparent over elastic-apm-traceparent

### [View the full list of issues and PRs](https://github.com/elastic/apm-agent-dotnet/issues?utf8=%E2%9C%93&q=label%3Av1.11.0)
```

## Install

```sh
dotnet tool install --global release-notes
```

On Linux (x64, arm64), Windows (x64, arm64) and macOS (arm64) this installs a self-contained native-AOT executable: no
shared .NET runtime, no JIT warm-up. Everywhere else it falls back to a framework-dependent build, which needs the .NET
runtime installed.

## Quick start

```sh
release-notes generate nullean my-repo --version 1.2.3 --token "$GITHUB_TOKEN" \
  --label "bug=Bug Fixes" --label "enhancement=New Features" --output release-notes-VERSION.md
```

Every command takes the repository owner and name, then `--version`, and needs the command named:

| Command | What it does |
|---|---|
| `generate` | prints the release notes, and writes them to `--output` too when given |
| `create-release` | creates or updates the GitHub release for the tag, with the `--body` files (repeatable) as its notes, and adds the version labels for the next release |
| `apply-labels` | adds the version labels for the next major, minor and patch release, and backport labels with `--backport-label-format` |
| `find-previous` | prints the release before `--version` |
| `current-version` | prints the current and the next version on separate lines for a `--query`: `M.N` (next patch), `M.x` (next minor) or `main`/`master` (next major) |

```console
$ release-notes find-previous elastic apm-agent-dotnet --version 1.11.0
1.10.0
$ release-notes current-version elastic apm-agent-dotnet --version 1.11.0 --query 1.x
1.14.1
1.15.0
```

Shared options:

| Option | What |
|---|---|
| `--token` | the GitHub token; anonymous otherwise, which a long issue list can exhaust |
| `--label <label>=<heading>` | map a label to a section (repeatable); replaces the default `bug=Bug Fixes`, `enhancement=New Features`, `documentation=Documentation Improvements` |
| `--uncategorized-header` | the heading for issues and pull requests none of the labels match |
| `--old-version` | the release to start from, instead of the one before `--version` |
| `--release-tag-format`, `--release-label-format` | how tags (`VERSION`) and version labels (`vVERSION`) are spelled; `VERSION` stands for the version |
| `--format` | `markdown` (default) or `asciidoc` |
| `--output` | also write the notes to this file; `VERSION` stands for the version |

With `--format asciidoc` nothing goes to standard out: `--output` is a directory, which gets
`release-notes-<version>-generated.asciidoc`, an empty `release-notes-<version>-human.asciidoc` for your own words (kept
if it exists), `release-notes-<version>.asciidoc` including both, and a `release-notes.asciidoc` index of the minor's
patch releases.

### GitHub Action

```yaml
- uses: nullean/release-notes@main
  with:
    command: generate
    owner: nullean
    repo: my-repo
    args: --version 1.2.3 --token ${{ secrets.GITHUB_TOKEN }}
```

The action runs `release-notes <command> <owner> <repo> <args>` in the prebuilt, distroless
`ghcr.io/nullean/release-notes:edge` container, so the workflow needs no .NET SDK. `command` is any of the commands
above; `args` passes through verbatim. Container actions run on Linux runners only.

### Container

The same image works anywhere `docker run` does, such as GitLab CI or a machine without the .NET SDK:

```sh
docker run --rm ghcr.io/nullean/release-notes:edge generate nullean my-repo --version 1.2.3 --token "$GITHUB_TOKEN"
```

It is native-AOT on a chiseled `runtime-deps` base, with no shell, running as a non-root user. `edge` tracks the latest
build of `main`; `latest` and the version tags (for example `0.11.5`) follow releases.

### Upgrading from 0.10 and earlier

0.11.0 moved the CLI onto [Nullean.Argh](https://github.com/nullean/argh), which is a breaking change:

- the command is always named: `release-notes <owner> <repo> ...` without `generate` no longer works;
- `--label` takes one `<label>=<heading>` token instead of two;
- every flag is kebab-case (`--oldversion` is now `--old-version`).

## Packages

The tool ships as one package per platform. Install the root package; the .NET SDK picks the right one.

| Package | What it is | |
|---|---|---|
| [`release-notes`](src/release-notes/README.md) | the tool; points the SDK at the package for your platform | [![NuGet](https://img.shields.io/nuget/v/release-notes?style=flat-square)](https://www.nuget.org/packages/release-notes) |
| `release-notes.<rid>` | the native-AOT build for `linux-x64`, `linux-arm64`, `win-x64`, `win-arm64` and `osx-arm64` | [![NuGet](https://img.shields.io/nuget/v/release-notes.linux-x64?style=flat-square)](https://www.nuget.org/packages/release-notes.linux-x64) |
| `release-notes.any` | the framework-dependent fallback (net8.0, net10.0) for every other platform | [![NuGet](https://img.shields.io/nuget/v/release-notes.any?style=flat-square)](https://www.nuget.org/packages/release-notes.any) |

## Learn more

- [Releases](https://github.com/nullean/release-notes/releases)
- [Contributing](https://github.com/nullean/.github/blob/main/CONTRIBUTING.md)

MIT licensed. Copyright Nullean and contributors.
