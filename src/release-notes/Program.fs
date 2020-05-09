open Argu
open Fake.Core
open System;
open System.Collections.Generic
open System.Linq
open System.Text
open System.Text.RegularExpressions
open Octokit

type Arguments =
    | [<MainCommand; Mandatory; CliPrefix(CliPrefix.None)>] Repository of owner:string * repository_name:string
    | Label of label:string * description:string
    | Token of string
    | Version of string
    | OldVersion of string
    | ReleaseLabel of string
    | ReleaseLabelFormat of string
    | UncategorizedHeader of string
    with
    interface IArgParserTemplate with
        member s.Usage =
            match s with
            | Repository _ -> "Repository to use in <owner> <repos_name> format"
            | Label _ -> "Map Github labels to categorizations, format <label> <description>, can be specified multiple times"
            | Token _ -> "The github token to use, if the issue list is long this may be necessary, defaults to anonymoys"
            | Version _ -> "The version that we are generating release notes for"
            | OldVersion _ -> "The previous version to generates release notes since"
            | ReleaseLabel _ -> "The version label on the issues / github prs, defaults to v[VERSION]"
            | ReleaseLabelFormat _ -> "x"
            | UncategorizedHeader _ -> "The header to use in the markdown for uncategorized issues/prs"

type GitHub(owner, repository) =
    member this.Owner = owner
    member this.Repository = repository
    member this.Url =
        sprintf "https://github.com/%s/%s/" this.Owner this.Repository
        
type ReleaseNotesConfig =
    {
        GitHub: GitHub
        Labels: Map<string, string>
        Token: string option
        Version: string
        OldVersion: string option
        ReleaseLabel: string 
        UncategorizedLabel: string 
        UncategorizedHeader: string 
    }


let issueNumberRegex(url: string) =
    let pattern = sprintf "\s(?:#|%sissues/)(?<num>\d+)" url
    Regex(pattern, RegexOptions.Multiline ||| RegexOptions.IgnoreCase ||| RegexOptions.CultureInvariant ||| RegexOptions.ExplicitCapture ||| RegexOptions.Compiled)
  
type GitHubItem(issue: Issue, relatedIssues: int list) =  
    member val Issue = issue
    member val RelatedIssues = relatedIssues
    member this.Title =
        let builder = StringBuilder("#")
                          .Append(issue.Number)
                          .Append(" ")       
        if issue.PullRequest = null then
            builder.AppendFormat("[ISSUE] {0}", issue.Title)
        else
            builder.Append(issue.Title) |> ignore
            if relatedIssues.Length > 0 then
                relatedIssues
                |> List.map(fun i -> sprintf "#%i" i)
                |> String.concat ", "
                |> sprintf " (%s: %s)" (if relatedIssues.Length = 1 then "issue" else "issues")
                |> builder.Append
            else builder
        |> ignore                  
        builder.ToString()
        
    member this.Labels = issue.Labels   
    member this.Number = issue.Number

let groupByLabel (config:ReleaseNotesConfig) (items: List<GitHubItem>) =
    let dict = Dictionary<string, GitHubItem list>()     
    for item in items do
        let mutable categorized = false
        // if an item is categorized with multiple config labels, it'll appear multiple times, once under each label
        for label in config.Labels do
            if item.Labels.Any(fun l -> l.Name = label.Key) then
                let exists,list = dict.TryGetValue(label.Key)
                match exists with 
                | true -> dict.[label.Key] <- item :: list
                | false -> dict.Add(label.Key, [item])
                categorized <- true
                
        if categorized = false then
            let exists,list = dict.TryGetValue(config.UncategorizedLabel)
            match exists with 
            | true ->                  
                match List.tryFind(fun (i:GitHubItem)-> i.Number = item.Number) list with 
                | Some _ -> ()
                | None -> dict.[config.UncategorizedLabel] <- item :: list                         
            | false -> dict.Add(config.UncategorizedLabel, [item])
    dict
    
let filterByPullRequests (issueNumberRegex: Regex) (issues:IReadOnlyList<Issue>): List<GitHubItem> =
    let extractRelatedIssues(issue: Issue) =
        let matches = issueNumberRegex.Matches(issue.Body)
        if matches.Count = 0 then list.Empty
        else         
            matches
            |> Seq.cast<Match>
            |> Seq.filter(fun m -> m.Success)
            |> Seq.map(fun m -> m.Groups.["num"].Value |> int)
            |> Seq.toList
    
    let collectedIssues = List<GitHubItem>()
    let items = List<GitHubItem>()
    
    for issue in issues do
        if issue.PullRequest <> null then
            let relatedIssues = extractRelatedIssues issue
            items.Add(GitHubItem(issue, relatedIssues))
        else
            collectedIssues.Add(GitHubItem(issue, list.Empty))
         
    // remove all issues that are referenced by pull requests            
    for pullRequest in items do
        for relatedIssue in pullRequest.RelatedIssues do
            collectedIssues.RemoveAll(fun i -> i.Issue.Number = relatedIssue) |> ignore
            
    // any remaining issues do not have an associated pull request, so add them
    items.AddRange(collectedIssues)       
    items
    
let getClosedIssues (config:ReleaseNotesConfig) =
    let issueNumberRegex = issueNumberRegex config.GitHub.Url   
    let filter = RepositoryIssueRequest()
    filter.Labels.Add config.ReleaseLabel
    filter.State <- ItemStateFilter.Closed

    let client = GitHubClient(ProductHeaderValue("ReleaseNotesGenerator"))
    
    client.Credentials <- 
        match config.Token with
        | Some token -> Credentials(token)
        | None -> Credentials.Anonymous
    
    client.Issue.GetAllForRepository(config.GitHub.Owner, config.GitHub.Repository, filter)
    |> Async.AwaitTask
    |> Async.RunSynchronously
    |> filterByPullRequests issueNumberRegex
    |> groupByLabel config
            
let run (config:ReleaseNotesConfig) =
    let gitHub = config.GitHub
    
    let client = GitHubClient(ProductHeaderValue("ReleaseNotesGenerator"))
    client.Credentials <- 
        match config.Token with
        | Some token -> Credentials(token)
        | None -> Credentials.Anonymous
    
    let oldVersion =
        match config.OldVersion with
        | Some v -> v
        | None ->
            let semVerVersion = SemVer.parse config.Version
            let foundOldVersion =
                client.Repository.Release.GetAll(config.GitHub.Owner, config.GitHub.Repository)
                |> Async.AwaitTask
                |> Async.RunSynchronously
                |> Seq.filter(fun t -> SemVer.isValid t.TagName)
                |> Seq.map(fun t -> SemVer.parse t.TagName)
                |> Seq.filter(fun v -> v < semVerVersion)
                |> Seq.sortByDescending(fun v -> v)
                |> Seq.tryHead
            match foundOldVersion with | Some v -> v.ToString() | _ -> failwith "No previous version found!"
                
    try      
        use writer = Console.Out
        writer.WriteLine(sprintf "%scompare/%s...%s" gitHub.Url oldVersion config.Version )
        writer.WriteLine()
        let closedIssues = getClosedIssues config
        for closedIssue in closedIssues do
            config.Labels.[closedIssue.Key] |> sprintf "## %s" |> writer.WriteLine    
            writer.WriteLine()
            for issue in closedIssue.Value do
                sprintf "- %s" issue.Title |> writer.WriteLine
            writer.WriteLine()
              
        sprintf "### [View the full list of issues and PRs](%sissues?utf8=%%E2%%9C%%93&q=label%%3A%s)" config.GitHub.Url config.ReleaseLabel
        |> writer.WriteLine   
        0
    with
    | ex ->
        Console.Error.Write ex
        1
    
[<EntryPoint>]
let main argv =
    
    let parser = ArgumentParser.Create<Arguments>(programName = "release-notes")
    let parsed = 
        try Some <| parser.ParseCommandLine(inputs = argv, raiseOnUsage = true)
        with e ->
            printfn "%s" e.Message
            None
    match parsed with
    | None -> 2
    | Some p ->
        try
            let token = p.TryGetResult Token
            let (owner, repos) = p.GetResult Repository
            let version = p.GetResult Version
            let oldVersion = p.TryGetResult OldVersion
            
            let uncategorizedLabel = "Uncategorized" 
            let uncategorizedHeader = p.TryGetResult UncategorizedHeader |> Option.defaultValue uncategorizedLabel
            
            let labels =
                p.GetResults Label @ [(uncategorizedLabel, uncategorizedHeader)]
                |> Map.ofList
            
            printfn "%A" labels
            let config = {
                GitHub = GitHub(owner, repos)
                Labels = labels 
                Token = token
                Version = version
                OldVersion = oldVersion
                ReleaseLabel = p.TryGetResult ReleaseLabel |> Option.defaultValue (sprintf "v%s" version)
                UncategorizedLabel = uncategorizedLabel
                UncategorizedHeader = uncategorizedHeader 
            }
            run config
        with e ->
            Console.ForegroundColor <- ConsoleColor.Red
            eprintfn "%s" e.Message
            Console.ResetColor()
            1
