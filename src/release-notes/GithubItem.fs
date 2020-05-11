namespace ReleaseNotes

open System.Text
open Octokit

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

