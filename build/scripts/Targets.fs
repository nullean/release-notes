module Targets

open Argu
open System
open System.IO
open Bullseye
open CommandLine
open Fake.Tools.Git
open ProcNet


    
let exec binary args =
    let r = Proc.Exec (binary, args |> List.map (fun a -> sprintf "\"%s\"" a) |> List.toArray)
    match r.HasValue with | true -> r.Value | false -> failwithf "invocation of `%s` timed out" binary
    
let private restoreTools = lazy(exec "dotnet" ["tool"; "restore"])
let private currentVersion =
    lazy(
        restoreTools.Value |> ignore
        let r = Proc.Start("dotnet", "minver", "-d", "canary")
        let o = r.ConsoleOut |> Seq.find (fun l -> not(l.Line.StartsWith("MinVer:")))
        o.Line
    )

let private clean (arguments:ParseResults<Arguments>) =
    if (Paths.Output.Exists) then Paths.Output.Delete (true)
    exec "dotnet" ["clean"] |> ignore
    
let private build (arguments:ParseResults<Arguments>) = exec "dotnet" ["build"; "-c"; "Release"] |> ignore

let private pristineCheck (arguments:ParseResults<Arguments>) =
    match Information.isCleanWorkingCopy "." with
    | true  -> printfn "The checkout folder does not have pending changes, proceeding"
    | _ -> failwithf "The checkout folder has pending changes, aborting"

let private generatePackages (arguments:ParseResults<Arguments>) =
    let output = Paths.RootRelative Paths.Output.FullName
    exec "dotnet" ["pack"; "-c"; "Release"; "-o"; output] |> ignore
    
let private validatePackages (arguments:ParseResults<Arguments>) =
    let nugetPackage =
        let p = Paths.Output.GetFiles("*.nupkg") |> Seq.sortByDescending(fun f -> f.CreationTimeUtc) |> Seq.head
        Paths.RootRelative p.FullName
    exec "dotnet" ["nupkg-validator"; nugetPackage; "-v"; currentVersion.Value; "-a"; Paths.ToolName; "-k"; "96c599bbe3e70f5d"] |> ignore
    
let private generateReleaseNotes (arguments:ParseResults<Arguments>) =
    let project = Paths.RootRelative Paths.ToolProject.FullName
    let currentVersion = currentVersion.Value
    let output =
        Paths.RootRelative <| Path.Combine(Paths.Output.FullName, sprintf "release-notes-%s.md" currentVersion)
    let dotnetRun =[ "run"; "-c"; "Release"; "-f"; "netcoreapp3.1"; "-p"; project]
    let tokenArgs =
        match (Fake.Core.Environment.environVarOrNone "GITHUB_TOKEN") with
        | None -> []
        | Some token -> ["--token"; token; "--newversionlabels true"]
    let validationArgs =
        (Paths.Repository.Split("/") |> Seq.toList)
        @ ["--version"; currentVersion
           "--label"; "enhancements"; "New Features"
           "--label"; "bug"; "Bug Fixes"
           "--label"; "documentation"; "Docs Improvements"
        ] @ tokenArgs
        @ ["--output"; output]
        
    exec "dotnet" (dotnetRun @ ["--"] @ validationArgs) |> ignore
    
let private release (arguments:ParseResults<Arguments>) =
    let output = Paths.RootRelative Paths.Output.FullName
    exec "dotnet" ["pack"; "-c"; "Release"; "-o"; output] |> ignore
    
let private publish (arguments:ParseResults<Arguments>) =
    // TODO
    // run release notes generator
    // run assembly-differ github comments
    // Combine the two outputs and publish
    // git tag -a version -m <combined output?"
    // git push --tags
    printfn "publish" 

let Setup (parsed:ParseResults<Arguments>) (subCommand:Arguments) =
    let step (name:string) action = Targets.Target(name, new Action(fun _ -> action(parsed)))
    
    let cmd (name:string) commandsBefore steps action =
        let singleTarget = (parsed.TryGetResult SingleTarget |> Option.defaultValue false)
        let deps =
            match (singleTarget, commandsBefore) with
            | (true, _) -> [] 
            | (_, Some d) -> d
            | _ -> []
        let steps = steps |> Option.defaultValue []
        Targets.Target(name, deps @ steps, Action(action))
        
    step Clean.Name clean
    cmd Build.Name None (Some [Clean.Name]) <| fun _ -> build parsed
    
    step PristineCheck.Name pristineCheck
    step GeneratePackages.Name generatePackages 
    step ValidatePackages.Name validatePackages 
    step GenerateReleaseNotes.Name generateReleaseNotes
    cmd Release.Name
        (Some [PristineCheck.Name; Build.Name;])
        (Some [GeneratePackages.Name; ValidatePackages.Name; GenerateReleaseNotes.Name])
        <| fun _ -> release parsed
        
    step PublishRelease.Name <| fun _ -> ignore()
    step PublishNewLabels.Name <| fun _ -> ignore()
    cmd Publish.Name
        (Some [Release.Name])
        None
        <| fun _ -> publish parsed
