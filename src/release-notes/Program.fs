module ReleaseNotes.Program

open System
open System.IO
open System.Text
open System.Text.RegularExpressions
open Argu
open Fake.Core
open Octokit
open ReleaseNotes
open ReleaseNotes.Arguments

let private createRelease (config:ReleaseNotesConfig) (client:GitHubClient) =
    let files =
        match config.ReleaseBodyFiles with
        | None -> []
        | Some s ->
            s
            |> List.map(fun f -> Path.GetFullPath(f))
            |> List.map(fun f -> (File.Exists(f) , f))
    
    let unknownFiles = files |> List.filter(fun (found, f) -> not found) |> List.map(fun (_, f) -> f)
    if unknownFiles.Length > 0 then
        failwithf "The following files were not found and can not be read to include in the release body: %A" unknownFiles
        
    let body =
        let body = files |> List.fold (fun (state:StringBuilder) (found, f) -> state.AppendLine(File.ReadAllText(f))) (StringBuilder())
        body.ToString()
    
    let existing =
        try
            Some <|
                (client.Repository.Release.Get(config.GitHub.Owner, config.GitHub.Repository, config.Version)
                |> Async.AwaitTask
                |> Async.RunSynchronously)
        with _ -> None
            
    match existing with
    | Some s ->
        printfn "Found release"
        client.Repository.Release.Edit(
            config.GitHub.Owner, config.GitHub.Repository, s.Id,
            ReleaseUpdate(Body=body))
            |> Async.AwaitTask
            |> Async.RunSynchronously
    | None ->
        client.Repository.Release.Create(
            config.GitHub.Owner, config.GitHub.Repository,
            NewRelease(config.Version, Body=body)) 
            |> Async.AwaitTask
            |> Async.RunSynchronously
    
    
let private locateOldVersion (config:ReleaseNotesConfig) (client:GitHubClient) =
    match config.OldVersion with
    | Some v -> Some v
    | None ->
        let semVerVersion = SemVer.parse config.Version
        let releases =
            client.Repository.Release.GetAll(config.GitHub.Owner, config.GitHub.Repository)
            |> Async.AwaitTask
            |> Async.RunSynchronously
        let foundOldVersion =
            releases
            |> Seq.filter(fun t -> SemVer.isValid t.TagName)
            |> Seq.map(fun t -> SemVer.parse t.TagName)
            |> Seq.filter(fun v -> v < semVerVersion)
            |> Seq.sortByDescending(fun v -> v)
            |> Seq.tryHead
        match releases.Count, foundOldVersion with
        | 0, _ -> None
        | _, Some v -> Some (v.ToString())
        | _ -> failwith "No previous version found!"
        
let private findCurrentAndNextVersion (config:ReleaseNotesConfig) (client:GitHubClient) versionQuery =
    let releases =
        client.Repository.Release.GetAll(config.GitHub.Owner, config.GitHub.Repository)
        |> Async.AwaitTask
        |> Async.RunSynchronously
    
    let minVersion = 
            match versionQuery with
            | "master" | "main" -> SemVer.parse "999.999.999"
            | query when Regex.IsMatch(query, "\d+\.x") ->
                SemVer.parse <| query.Replace(".x", ".0") + ".0"
            | query when Regex.IsMatch(query, "\d+\.\d+") ->
                SemVer.parse <| query + ".0"
            | query when Regex.IsMatch(query, "\d+\.\d+.0") ->
                SemVer.parse <| query
            | _ ->
                failwithf "%s is not a valid version query" versionQuery
    
    let re =
        let prefix =
            match config.ReleaseLabelFormat.Replace("VERSION", "") with
            | p when String.IsNullOrWhiteSpace p -> ""
            | p -> sprintf "(?:%s)?" p
        Regex <| sprintf @"%s(\d+\.\d+\.\d+(?:-\w+)?)" prefix
    let foundOldVersion =
        releases
        |> Seq.choose(fun t ->
            match re.Match(t.TagName).Groups |> Seq.toList with
            | [_; capture] -> Some capture.Value
            | _ -> None
        )
        |> Seq.filter(SemVer.isValid)
        |> Seq.map(SemVer.parse)
        |> Seq.filter(fun v ->
            match versionQuery with
            | "master" | "main" -> true
            | query when Regex.IsMatch(query, "\d+\.x") ->
                v.Major = minVersion.Major
            | query when Regex.IsMatch(query, "\d+\.\d+") ->
                v.Major = minVersion.Major && v.Minor = minVersion.Minor
            | query when Regex.IsMatch(query, "\d+\.\d+.0") ->
                v.Major = minVersion.Major && v.Minor = minVersion.Minor
            | _ ->
                false
        )
        |> Seq.sortByDescending(fun v -> v)
        |> Seq.tryHead
    match releases.Count, foundOldVersion with
    | 0, _ -> None
    | _, Some v ->
        match versionQuery with
        | "master" | "main" ->
            let nextVersion = SemVer.parse <| sprintf "%i.0.0" (v.Major + 1u)
            Some <| (v, nextVersion)
        | query when Regex.IsMatch(query, "\d+\.x") ->
            let nextVersion = SemVer.parse <| sprintf "%i.%i.0" v.Major (v.Minor + 1u)
            Some <| (v, nextVersion)
        | query when Regex.IsMatch(query, "\d+\.\d+") ->
            let nextVersion = SemVer.parse <| sprintf "%i.%i.%i" v.Major v.Minor (v.Patch + 1u)
            Some <| (v, nextVersion)
        | query when Regex.IsMatch(query, "\d+\.\d+.0") ->
            let nextVersion = SemVer.parse <| sprintf "%i.%i.%i" v.Major v.Minor (v.Patch + 1u)
            Some <| (v, nextVersion)
        | _ ->
            failwithf "%s is not a valid version query" versionQuery
    | _ -> failwith "No current version found!"

let private writeMarkDownReleaseNotes (config:ReleaseNotesConfig) (client:GitHubClient) oldVersion =
    let releasedLabel = Labeler.releaseLabel config.Version config.ReleaseLabelFormat  
    let gitHub = config.GitHub
    use writer = new OutputWriter(config.Output)
    //oldVersion can be none if the repository has never had a release
    oldVersion |> Option.iter(fun oldVersion ->
        writer.WriteLine (sprintf "%scompare/%s...%s" gitHub.Url oldVersion config.Version )
        writer.EmptyLine ()
    )
    let closedIssues = GithubScanner.getClosedIssues config client releasedLabel
    for closedIssue in closedIssues do
        config.Labels.[closedIssue.Key] |> sprintf "## %s" |> writer.WriteLine    
        writer.EmptyLine ()
        for issue in closedIssue.Value do
            sprintf "- %s" issue.Title |> writer.WriteLine
        writer.EmptyLine ()
    
    sprintf "### [View the full list of issues and PRs](%sissues?utf8=%%E2%%9C%%93&q=label%%3A%s)" config.GitHub.Url releasedLabel
    |> writer.WriteLine
    writer.ToString()

let private writeReleaseNotes (config:ReleaseNotesConfig) (client:GitHubClient) oldVersion =
    writeMarkDownReleaseNotes config client oldVersion

let run (config:ReleaseNotesConfig) =
    let client = GitHubClient(ProductHeaderValue("ReleaseNotesGenerator"))
    client.Credentials <- 
        match config.Token with
        | Some token -> Credentials(token)
        | None -> Credentials.Anonymous
    
    try      
        match (config.OldVersionOnly, config.GenerateReleaseOnGithub, config.ApplyLabels, config.VersionQuery) with
        | (true, _, _, _) ->
            let oldVersion = locateOldVersion config client
            printfn "%s" (oldVersion |> Option.defaultValue "")
        | (_, true, _, _) ->
               let oldVersion = locateOldVersion config client
               let release = createRelease config client 
               Labeler.addNewVersionLabels config client
               writeReleaseNotes config client oldVersion |> ignore
        | (_, _, true, _) ->
            Labeler.addNewVersionLabels config client 
            Labeler.addBackportLabels config client 
        | (false, false, false, Some versionQuery) ->
               match findCurrentAndNextVersion config client versionQuery with
               | Some (current, next) ->
                   printfn "%O" current
                   printfn "%O" next
               | _ ->
                   printfn "%O" config.Version
        | (false, false, false, None) ->
               let oldVersion = locateOldVersion config client
               writeReleaseNotes config client oldVersion |> ignore
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
            
            let oldVersionOnly = match p.TryGetSubCommand() with | Some FindPreviousVersion -> true | _ -> false 
            let generateRelease = match p.TryGetSubCommand() with | Some (CreateRelease _) -> true | _ -> false 
            let applyLabels = match p.TryGetSubCommand() with | Some ApplyLabels -> true | _ -> false 
            let versionQuery =
                match p.TryGetSubCommand() with
                | Some (CurrentVersion v) -> Some <| v.GetResult Query
                | _ -> None 
            
            let labels =
                p.GetResults Label @ [(uncategorizedLabel, uncategorizedHeader)]
                |> Map.ofList
                
            let bodyFilePaths =
                match p.TryGetSubCommand() with
                | Some (CreateRelease a) -> Some <| a.GetResults BodyFilePath
                | _ -> None 
            
            let config = {
                GitHub = GitHubRepository(owner, repos)
                Labels = labels 
                ApplyLabels = applyLabels
                Token = token
                Version = version
                OldVersion = oldVersion
                ReleaseLabelFormat = p.TryGetResult ReleaseLabelFormat |> Option.defaultValue "vVERSION"
                BackportLabelFormat = p.TryGetResult BackportLabelFormat
                UncategorizedLabel = uncategorizedLabel
                UncategorizedHeader = uncategorizedHeader
                Output = p.TryGetResult Output
                // TODO parameter
                LabelColor = "e3e3e3"
                OldVersionOnly = oldVersionOnly
                GenerateReleaseOnGithub = generateRelease
                ReleaseBodyFiles = bodyFilePaths
                VersionQuery = versionQuery
            }
            run config
        with e ->
            Console.ForegroundColor <- ConsoleColor.Red
            eprintfn "%s" e.Message
            Console.ResetColor()
            1
