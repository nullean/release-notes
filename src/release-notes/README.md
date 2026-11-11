# release-notes

A .NET tool that writes release notes for a GitHub repository from the issues and pull requests labelled with the
version being released (`v1.2.3` by default), grouped into sections by their other labels.

```bash
dotnet tool install --global release-notes
release-notes generate nullean my-repo --version 1.2.3 --label "bug=Bug Fixes" --label "enhancement=New Features" \
  --output release-notes-VERSION.md
```

## Commands

Every command takes the repository owner and name, then `--version`.

| Command | What it does |
|---|---|
| `generate` | prints the release notes, and writes them to `--output` too when given |
| `create-release` | creates or updates the GitHub release for the tag, with the `--body` files as its notes, and adds the version labels for the next release |
| `apply-labels` | adds the version labels for the next major, minor and patch release, and backport labels with `--backport-label-format` |
| `find-previous` | prints the release before `--version` |
| `current-version` | prints the current and next version for a `--query` (`M.N`, `M.x`, or `main`/`master`) |

`--token` authenticates with GitHub (anonymous otherwise, which a long issue list can exhaust). `--label
<label>=<heading>` maps a label to a section, and defaults to bug, enhancement and documentation. `--format` is
`Markdown` or `AsciiDoc`; `--release-tag-format` and `--release-label-format` set how tags and version labels are
spelled, with `VERSION` standing for the version.

On Linux, Windows and macOS (arm64) the tool installs as a native-AOT binary; elsewhere it falls back to a
framework-dependent build. It also runs as a container (`ghcr.io/nullean/release-notes`) and as a GitHub Action: see
https://github.com/nullean/release-notes.
