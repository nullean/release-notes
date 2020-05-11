module ReleaseNotes.Program

open System
open Argu
open Fake.Core
open Octokit
open ReleaseNotes.Arguments

let private releaseLabel version (format:string) = format.Replace("VERSION", version)

let private addNewVersionLabels (config:ReleaseNotesConfig) (client:GitHubClient) =
    let create label =
        client.Issue.Labels.Create(
            config.GitHub.Owner, config.GitHub.Repository,
            NewLabel(label, config.LabelColor, Description=sprintf "%s" label))
        |> ignore
    
    let v = SemVer.parse config.Version
    let newMajor = sprintf "%i.0.0" (v.Major+1u)
    let newMinor = sprintf "%i.%i.0" v.Major (v.Minor+1u)
    let newPatch = sprintf "%i.%i.%i" v.Major (v.Minor) (v.Patch+1u)
    
    create newMajor
    create newMinor
    create newPatch
    
let private locateOldVersion (config:ReleaseNotesConfig) (client:GitHubClient) =
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
                
let private writeReleaseNotes (config:ReleaseNotesConfig) (client:GitHubClient) oldVersion =
    let releasedLabel = releaseLabel config.Version config.ReleaseLabelFormat  
    let gitHub = config.GitHub
    use writer = new OutputWriter(config.Output)
    writer.WriteLine (sprintf "%scompare/%s...%s" gitHub.Url oldVersion config.Version )
    writer.EmptyLine ()
    let closedIssues = GithubScanner.getClosedIssues config client releasedLabel
    for closedIssue in closedIssues do
        config.Labels.[closedIssue.Key] |> sprintf "## %s" |> writer.WriteLine    
        writer.EmptyLine ()
        for issue in closedIssue.Value do
            sprintf "- %s" issue.Title |> writer.WriteLine
        writer.EmptyLine ()
    
    sprintf "### [View the full list of issues and PRs](%sissues?utf8=%%E2%%9C%%93&q=label%%3A%s)" config.GitHub.Url releasedLabel
    |> writer.WriteLine   

let run (config:ReleaseNotesConfig) =
    let client = GitHubClient(ProductHeaderValue("ReleaseNotesGenerator"))
    client.Credentials <- 
        match config.Token with
        | Some token -> Credentials(token)
        | None -> Credentials.Anonymous
    
    try      
        let oldVersion = locateOldVersion config client
        writeReleaseNotes config client oldVersion
        if (config.NewVersionLabels) then addNewVersionLabels config client
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
            
            let (newVersionLabels, labelColor) = p.TryGetResult NewVersionLabels |> Option.defaultValue (false, None)
            
            let labels =
                p.GetResults Label @ [(uncategorizedLabel, uncategorizedHeader)]
                |> Map.ofList
            
            printfn "%A" labels
            let config = {
                GitHub = GitHubRepository(owner, repos)
                Labels = labels 
                Token = token
                Version = version
                OldVersion = oldVersion
                ReleaseLabelFormat = p.TryGetResult ReleaseLabelFormat |> Option.defaultValue "vVERSION"
                UncategorizedLabel = uncategorizedLabel
                UncategorizedHeader = uncategorizedHeader
                Output = p.TryGetResult Output
                NewVersionLabels = newVersionLabels
                LabelColor = labelColor |> Option.defaultValue "#e3e3e3"
            }
            run config
        with e ->
            Console.ForegroundColor <- ConsoleColor.Red
            eprintfn "%s" e.Message
            Console.ResetColor()
            1
