module Targets

open Argu
open System
open System.IO
open Bullseye
open CommandLine
open Fake.Tools.Git
open ProcNet


    
let exec binary args =
    let r = Proc.Exec (binary, args |> List.toArray)
    match r.HasValue with | true -> r.Value | false -> failwithf "invocation of `%s` timed out" binary
    
let private restoreTools = lazy(exec "dotnet" ["tool"; "restore"])
let private currentVersion =
    lazy(
        restoreTools.Value |> ignore
        let r = Proc.Start("dotnet", "minver", "-d", "canary")
        let o = r.ConsoleOut |> Seq.find (fun l -> not(l.Line.StartsWith("MinVer:")))
        o.Line
    )

let private bump (arguments:ParseResults<BumpArguments>) =
    printfn "Not needed only integration branch is master"
    
let private build (arguments:ParseResults<Arguments>) =
    if (Paths.Output.Exists) then Paths.Output.Delete (true)
    let result = exec "dotnet" ["clean"]
    let result = exec "dotnet" ["build"; "-c"; "Release"] 
    
    printfn "build"
    
let private pristineCheck (arguments:ParseResults<Arguments>) =
    match Information.isCleanWorkingCopy "." with
    | true  ->
        printfn "The checkout folder does not have pending changes, proceeding"
    | _ -> 
        failwithf "The checkout folder has pending changes, aborting"
    
    
let private validatePackages (arguments:ParseResults<Arguments>) =
    let nugetPackage =
        let p = Paths.Output.GetFiles("*.nupkg") |> Seq.sortByDescending(fun f -> f.CreationTimeUtc) |> Seq.head
        Paths.RootRelative p.FullName
    let project = Paths.RootRelative Paths.ToolProject.FullName
    let validate = exec "dotnet" ["nupkg-validator"; nugetPackage; "-v"; currentVersion.Value; "-a"; Paths.ToolName; "-k"; "96c599bbe3e70f5d"]
    
    printfn "validatePackages"
    
let private generateReleaseNotes (arguments:ParseResults<Arguments>) =
    let project = Paths.RootRelative Paths.ToolProject.FullName
    let output =
        Paths.RootRelative <| Path.Combine(Paths.Output.FullName, sprintf "release-notes-%s.md" currentVersion.Value)
    let validate =
        let dotnetRun =[ "run"; "-c"; "Release"; "-f"; "netcoreapp3.1"; "-p"; project]
        let tokenArgs =
            match (Fake.Core.Environment.environVarOrNone "GITHUB_TOKEN") with
            | None -> []
            | Some token -> ["--token"; token; "--newversionlabels"]
        let validationArgs =
            (Paths.ToolName.Split("/") |> Seq.toList)
            @ ["--version"; currentVersion.Value
               "--label"; "enhancements"; "New Features"
               "--label"; "bug"; "Bug Fixes"
               "--label"; "documentation"; "Docs Improvements"
            ] @ tokenArgs
            @ ["--output"; output]
            
        exec "dotnet" (dotnetRun @ ["--"] @ validationArgs)
    printfn "validatePackages"
    
let private release (arguments:ParseResults<Arguments>) =
    let output = Paths.RootRelative Paths.Output.FullName
    let publish = exec "dotnet" ["pack"; "-c"; "Release"; "-o"; output]
    
    printfn "release"
    
let private publish (arguments:ParseResults<Arguments>) =
    // TODO
    // run release notes generator
    // run assembly-differ github comments
    // Combine the two outputs and publish
    // git tag -a version -m <combined output?"
    // git push --tags
    printfn "publish" 

let Setup (parsed:ParseResults<Arguments>) (subCommand:Arguments) =
    let step name (action:ParseResults<Arguments> -> unit) =
        Targets.Target(name, [], Action(fun _ -> action(parsed)))
        name
    let cmd (name:string) dependentCommands steps action =
        let singleTarget = (parsed.TryGetResult SingleTarget |> Option.defaultValue false)
        let deps =
            match (singleTarget, dependentCommands) with
            | (true, _) -> [] 
            | (_, Some d) -> d
            | _ -> []
        let steps = steps |> Option.defaultValue []
        Targets.Target(name, deps @ steps, Action(action))
        
    cmd BumpArguments.Name None None <| fun _ ->
        match subCommand with | Bump b -> bump b | _ -> failwithf "bump needs bump args"
        
    cmd Build.Name None None <| fun _ -> build parsed
    
    cmd PristineCheck.Name None None <| fun _ -> pristineCheck parsed
    
    cmd Release.Name
        (Some [PristineCheck.Name; Build.Name])
        (Some [
            step "validatePackages" validatePackages;
            step "generateReleaseNotes" generateReleaseNotes;
        ])
        <| fun _ -> release parsed
        
    cmd Publish.Name (Some [Release.Name]) None <| fun _ -> publish parsed
