module ReleaseNotes.Labeler

open Fake.Core
open Octokit
open ReleaseNotes.Arguments

let releaseLabel version (format:string) = format.Replace("VERSION", version)
let backportLabel version (format:string) = format.Replace("BRANCH", version)

let private create (config:ReleaseNotesConfig) (client:GitHubClient) label =
    let existing =
        try
            Some <|
                (client.Issue.Labels.Get(config.GitHub.Owner, config.GitHub.Repository, label)
                |> Async.AwaitTask
                |> Async.RunSynchronously)
        with _ -> None
        
    match existing with
    | Some s -> ignore()
    | None ->
        client.Issue.Labels.Create(
            config.GitHub.Owner, config.GitHub.Repository,
            NewLabel(label, config.LabelColor))
            |> Async.AwaitTask
            |> Async.RunSynchronously
            |> ignore

let private existsBranch (config:ReleaseNotesConfig) (client:GitHubClient) branch =
    try
        Some <|
            (client.Repository.Branch.Get(config.GitHub.Owner, config.GitHub.Repository, branch)
            |> Async.AwaitTask
            |> Async.RunSynchronously)
    with _ -> None
    
let addNewVersionLabels (config:ReleaseNotesConfig) (client:GitHubClient) =
    let v = SemVer.parse config.Version
    let newMajor = sprintf "%i.0.0" (v.Major+1u)
    let newMinor = sprintf "%i.%i.0" v.Major (v.Minor+1u)
    let newPatch = sprintf "%i.%i.%i" v.Major (v.Minor) (v.Patch+1u)
    
    create config client <| releaseLabel newMajor config.ReleaseLabelFormat
    create config client <| releaseLabel newMinor config.ReleaseLabelFormat 
    create config client <| releaseLabel newPatch config.ReleaseLabelFormat
    
let addBackportLabels (config:ReleaseNotesConfig) (client:GitHubClient) =
    let mainExists = existsBranch config client "main"
    let masterExists = existsBranch config client "master"
    match (mainExists, masterExists) with 
    | (Some _, Some _)  ->
        create config client <| backportLabel "main" config.BackportLabelFormat
        create config client <| backportLabel "master" config.BackportLabelFormat
    | (Some _, _)  -> create config client <| backportLabel "main" config.BackportLabelFormat
    | (None, Some _)  -> create config client <| backportLabel "master" config.BackportLabelFormat
    | _ -> printfn "Repository does not have either main or master branch"
    
    let v = SemVer.parse config.Version
    let backportBranches = [
        sprintf "%i.x" (v.Major);
        sprintf "%i.%i" v.Major v.Minor;
        sprintf "%i.x" (v.Major+1u);
        sprintf "%i.%i" v.Major (v.Minor+1u);
    ] 
    for branch in backportBranches do
        match existsBranch config client branch with
        | Some _ -> create config client <| backportLabel branch config.BackportLabelFormat
        | None -> printfn "branch %s does not exist yet so skipping creating a backport label" branch
    
    

