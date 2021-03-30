module ReleaseNotes.Arguments

open Argu

type CreateReleaseArguments =
    | [<CustomCommandLine("--body")>]BodyFilePath of string  
    with
    interface IArgParserTemplate with
        member s.Usage =
            match s with
            | BodyFilePath _ -> "Path to file that will be read and used for the body for the release, can be specified multiple times to combine"
            
type CurrentVersionArguments =
    | [<CustomCommandLine("--query")>]Query of string  
    with
    interface IArgParserTemplate with
        member s.Usage =
            match s with
            | Query _ -> "An anchor query, M.N, M.x, or master. will find the current and next patch, minor, major respectfully"

type Format = Markdown | AsciiDoc

type Arguments =
    | [<MainCommand;Mandatory;Inherit; CliPrefix(CliPrefix.None);>] Repository of owner:string * repository_name:string
    | [<SubCommand; CustomCommandLine("apply-labels"); CliPrefix(CliPrefix.None)>] ApplyLabels 
    | [<SubCommand; CustomCommandLine("find-previous"); CliPrefix(CliPrefix.None);>] FindPreviousVersion 
    | [<SubCommand; CustomCommandLine("current-version"); CliPrefix(CliPrefix.None);>] CurrentVersion of ParseResults<CurrentVersionArguments>
    | [<SubCommand; CustomCommandLine("create-release"); CliPrefix(CliPrefix.None)>] CreateRelease of ParseResults<CreateReleaseArguments>
    | [<Inherit;Mandatory>]Version of string
    | [<Inherit>]Token of string
    | [<Inherit>]Label of label:string * description:string
    | OldVersion of string
    | ReleaseTagFormat of string
    | ReleaseLabelFormat of string
    | BackportLabelFormat of string
    | Format of Format
    | UncategorizedHeader of string
    | Output of string
    
    with
    interface IArgParserTemplate with
        member s.Usage =
            match s with
            | Version _ -> "Version that is being released"
            | Token _ -> "The github token to use, if the issue list is long this may be necessary, defaults to anonymous"
            
            | ApplyLabels _ -> "Creates version and backport labels"
            | FindPreviousVersion _ -> "Find the previous release for the passed version"
            | CurrentVersion _ -> "Given search syntax finds the current and the next versions on separate lines"
            | CreateRelease _ -> "Makes sure the tag exists as release on github and introduces new version labels for the next major/minor/patch"
            
            | Repository _ -> "Repository to use in <owner> <repos_name> format"
            | Label _ -> "Map Github labels to categorizations, format <label> <description>, can be specified multiple times"
            | OldVersion _ -> "The previous version to generate release notes since, optional the tool will find the previous release"
            | ReleaseTagFormat _ ->
                sprintf "The release tag format, defaults to VERSION, VERSION will be replaced by the actual version" 
            | ReleaseLabelFormat _ ->
                sprintf "The release label format, defaults to vVERSION, VERSION will be replaced by the actual version" 
            | BackportLabelFormat _ ->
                sprintf "The backport label format, defaults to 'Backport BRANCH`, BRANCH will be calculated from the version" 
            | UncategorizedHeader _ -> "The header to use in the markdown for uncategorized issues/prs"
            | Output _ -> "write the release notes to a file as well as standard out, VERSION will be replaced by the actual version"
            | Format _ -> "The format in which to print the results, can be markdown and asciidoc"

type GitHubRepository(owner, repository) =
    member this.Owner = owner
    member this.Repository = repository
    member this.Url =
        sprintf "https://github.com/%s/%s/" this.Owner this.Repository
        
type ReleaseNotesConfig =
    {
        GitHub: GitHubRepository
        Labels: Map<string, string>
        ApplyLabels: bool
        Token: string option
        Version: string
        OldVersion: string option
        ReleaseTagFormat: string 
        ReleaseLabelFormat: string 
        BackportLabelFormat: string option 
        UncategorizedLabel: string 
        UncategorizedHeader: string 
        Output: string option
        OldVersionOnly: bool
        LabelColor: string
        GenerateReleaseOnGithub: bool
        ReleaseBodyFiles: string list option
        VersionQuery: string option
        Format: Format
    }



