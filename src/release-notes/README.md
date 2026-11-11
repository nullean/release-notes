# release-notes

![nullean](https://raw.githubusercontent.com/nullean/nullean.github.io/main/assets/nullean-lockup-544x192.png)

A .NET tool that writes release notes for a GitHub repository from the issues and pull requests labelled with the
version being released (`v1.2.3` by default), grouped into sections by their other labels.

[![NuGet](https://img.shields.io/nuget/v/release-notes?style=flat-square)](https://www.nuget.org/packages/release-notes)

## Install

```sh
dotnet tool install --global release-notes
```

On Linux (x64, arm64), Windows (x64, arm64) and macOS (arm64) this installs a native-AOT executable from the
`release-notes.<rid>` package; everywhere else it falls back to the framework-dependent `release-notes.any`. Install the
`release-notes` package itself, not those.

## Quick start

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

Every command takes the repository owner and name, then `--version`:

| Command | What it does |
|---|---|
| `generate` | prints the release notes, and writes them to `--output` too when given |
| `create-release` | creates or updates the GitHub release for the tag, with the `--body` files as its notes, and adds the version labels for the next release |
| `apply-labels` | adds the version labels for the next major, minor and patch release, and backport labels with `--backport-label-format` |
| `find-previous` | prints the release before `--version` |
| `current-version` | prints the current and next version for a `--query` (`M.N`, `M.x`, or `main`/`master`) |

`--token` authenticates with GitHub (anonymous otherwise, which a long issue list can exhaust). `--label
<label>=<heading>` maps a label to a section, and defaults to bug, enhancement and documentation. `--format` is
`markdown` or `asciidoc`; `--release-tag-format` and `--release-label-format` set how tags and version labels are
spelled, with `VERSION` standing for the version.

It also runs as a GitHub Action (`uses: nullean/release-notes@main`) and as a container
(`ghcr.io/nullean/release-notes`), both without a .NET SDK.

## Learn more

- Source, GitHub Action and container usage: https://github.com/nullean/release-notes
- Releases: https://github.com/nullean/release-notes/releases
