module ReleaseNotes.GithubScanner

open Octokit
open System.Collections.Generic
open System.Linq
open ReleaseNotes.Arguments
open System.Text.RegularExpressions

let issueNumberRegex(url: string) =
    let pattern = sprintf "\s(?:#|%sissues/)(?<num>\d+)" url
    Regex(pattern, RegexOptions.Multiline ||| RegexOptions.IgnoreCase ||| RegexOptions.CultureInvariant ||| RegexOptions.ExplicitCapture ||| RegexOptions.Compiled)

let private groupByLabel (config:ReleaseNotesConfig) (items: List<GitHubItem>) =
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
    
let private filterByPullRequests (issueNumberRegex: Regex) (issues:IReadOnlyList<Issue>): List<GitHubItem> =
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
    
let getClosedIssues (config:ReleaseNotesConfig) (client:GitHubClient) releasedLabel =
    let issueNumberRegex = issueNumberRegex config.GitHub.Url   
    let filter = RepositoryIssueRequest()
    filter.Labels.Add releasedLabel
    filter.State <- ItemStateFilter.Closed

    client.Issue.GetAllForRepository(config.GitHub.Owner, config.GitHub.Repository, filter)
    |> Async.AwaitTask
    |> Async.RunSynchronously
    |> filterByPullRequests issueNumberRegex
    |> groupByLabel config

