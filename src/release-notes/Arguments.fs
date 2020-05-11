module ReleaseNotes.Arguments

open Argu

type CreateReleaseArguments =
    | [<CustomCommandLine("--body-path")>]BodyFilePath of string  
    with
    interface IArgParserTemplate with
        member s.Usage =
            match s with
            | BodyFilePath _ -> "Path to file that will be read and used for the body for the release, can be specified multiple times to combine"

type Arguments =
    | [<MainCommand;Mandatory;Inherit; CliPrefix(CliPrefix.None);>] Repository of owner:string * repository_name:string
    | [<SubCommand; CustomCommandLine("find-previous"); CliPrefix(CliPrefix.None);>] FindPreviousVersion 
    | [<SubCommand; CustomCommandLine("create-release"); CliPrefix(CliPrefix.None)>] CreateRelease of ParseResults<CreateReleaseArguments>
    | [<Inherit;Mandatory>]Version of string
    | [<Inherit>]Token of string
    | [<Inherit>]Label of label:string * description:string
    | OldVersion of string
    | ReleaseLabelFormat of string
    | UncategorizedHeader of string
    | Output of string
    
    with
    interface IArgParserTemplate with
        member s.Usage =
            match s with
            | Version _ -> "Version that is being released"
            | Token _ -> "The github token to use, if the issue list is long this may be necessary, defaults to anonymous"
            
            | FindPreviousVersion _ -> "Find the previous release for the passed version"
            | CreateRelease _ -> "Makes sure the tag exists as release on github and introduces new version labels for the next major/minor/patch"
            
            | Repository _ -> "Repository to use in <owner> <repos_name> format"
            | Label _ -> "Map Github labels to categorizations, format <label> <description>, can be specified multiple times"
            | OldVersion _ -> "The previous version to generate release notes since, optional the tool will find the previous release"
            | ReleaseLabelFormat _ ->
                sprintf "The release label format, defaults to vVERSION, VERSION will be replaced by the actual version" 
            | UncategorizedHeader _ -> "The header to use in the markdown for uncategorized issues/prs"
            | Output _ -> "write the release notes to a file as well as standard out, VERSION will be replaced by the actual version"

type GitHubRepository(owner, repository) =
    member this.Owner = owner
    member this.Repository = repository
    member this.Url =
        sprintf "https://github.com/%s/%s/" this.Owner this.Repository
        
type ReleaseNotesConfig =
    {
        GitHub: GitHubRepository
        Labels: Map<string, string>
        Token: string option
        Version: string
        OldVersion: string option
        ReleaseLabelFormat: string 
        UncategorizedLabel: string 
        UncategorizedHeader: string 
        Output: string option
        OldVersionOnly: bool
        LabelColor: string
        GenerateReleaseOnGithub: bool
        ReleaseBodyPaths: string list option
    }



