module ReleaseNotes.Arguments

open Argu

type Arguments =
    | [<MainCommand; Mandatory; CliPrefix(CliPrefix.None)>] Repository of owner:string * repository_name:string
    | Label of label:string * description:string
    | Token of string
    | Version of string
    | OldVersion of string
    | ReleaseLabelFormat of string
    | UncategorizedHeader of string
    | Output of string
    | NewVersionLabels of bool
    with
    interface IArgParserTemplate with
        member s.Usage =
            match s with
            | Repository _ -> "Repository to use in <owner> <repos_name> format"
            | Label _ -> "Map Github labels to categorizations, format <label> <description>, can be specified multiple times"
            | Token _ -> "The github token to use, if the issue list is long this may be necessary, defaults to anonymoys"
            | Version _ -> "The version that we are generating release notes for"
            | OldVersion _ -> "The previous version to generates release notes since"
            | ReleaseLabelFormat _ ->
                sprintf "The release label format, defaults to vVERSION, VERSION will be replaced by the actual version" 
            | UncategorizedHeader _ -> "The header to use in the markdown for uncategorized issues/prs"
            | Output _ -> "write the release notes to a file as well as standard out, VERSION will be replaced by the actual version"
            | NewVersionLabels _ -> "ensure new major, minor and patch version labels exist after the released version"

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
        NewVersionLabels: bool
        LabelColor: string
    }



