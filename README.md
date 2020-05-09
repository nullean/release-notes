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

## Run 

```bat
dotnet release-notes
```

You can omit `dotnet` if you install this as a global tool

```bat
USAGE: release-notes [--help] [--label <label> <description>] [--token <string>] [--version <string>]
                     [--oldversion <string>] [--releaselabel <string>] [--uncategorizedheader <string>]
                     <owner> <repository name>

REPOSITORY:

    <owner> <repository name>
                          Repository to use in <owner> <repos_name> format

OPTIONS:

    --label <label> <description>
                          Map Github labels to categorizations, format <label> <description>, can be specified
                          multiple times
    --token <string>      The github token to use, if the issue list is long this may be necessary, defaults to
                          anonymoys
    --version <string>    The version that we are generating release notes for
    --oldversion <string> The previous version to generates release notes since
    --releaselabel <string>
                          The version label on the issues / github prs, defaults to v[VERSION]
    --uncategorizedheader <string>
                          The header to use in the markdown for uncategorized issues/prs
    --help                display this list of options.
```

#### Examples:

