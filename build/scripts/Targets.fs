module Targets

open Argu
open System
open Bullseye
open CommandLine
open Fake.Tools.Git
open ProcNet

    
let exec binary args =
    let r = Proc.Exec (binary, args |> List.toArray)
    match r.HasValue with | true -> r.Value | false -> failwithf "invocation of `%s` timed out" binary 

let private bump (arguments:ParseResults<BumpArguments>) =
    printfn "Not needed only integration branch is master"
    
let private build (arguments:ParseResults<Arguments>) =
    if (Paths.Output.Exists) then Paths.Output.Delete (true)
    let result = exec "dotnet" ["clean"]
    let result = exec "dotnet" ["build"; "-c"; "Release"] 
    
    printfn "build"
    
let private pristineCheck (arguments:ParseResults<Arguments>) =
    let clean = Information.isCleanWorkingCopy "."
    match clean with
    | true  ->
        printfn "The checkout folder does not have pending changes, proceeding"
    | _ -> 
        failwithf "The checkout folder has pending changes, aborting"
    
let private release (arguments:ParseResults<Arguments>) =
    let output = Paths.RootRelative Paths.Output.FullName
    let publish = exec "dotnet" ["pack"; "-c"; "Release"; "-o"; output]
    
    let toolRestore = exec "dotnet" ["tool"; "restore"]
    
    let currentVersion =
        let r = Proc.Start("dotnet", "minver", "-d", "canary")
        let o = r.ConsoleOut |> Seq.find (fun l -> not(l.Line.StartsWith("MinVer:")))
        o.Line
    
    let nugetPackage =
        let p = Paths.Output.GetFiles("*.nupkg") |> Seq.sortByDescending(fun f -> f.CreationTimeUtc) |> Seq.head
        Paths.RootRelative p.FullName
    let project = Paths.RootRelative Paths.ToolProject.FullName
    let validate = exec "dotnet" ["nupkg-validator"; nugetPackage; "-v"; currentVersion; "-a"; Paths.ToolName; "-k"; "96c599bbe3e70f5d"]
    
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
    let cmd (name:string) dependencies action =
        let deps =
            match (parsed.TryGetResult SingleTarget |> Option.defaultValue false) with
            | true -> []
            | _ -> dependencies
        Targets.Target(name, deps, Action(action))
    
    cmd BumpArguments.Name [] <| fun _ ->
        match subCommand with | Bump b -> bump b | _ -> failwithf "bump needs bump args"
    cmd Build.Name [] <| fun _ -> build parsed
    
    cmd PristineCheck.Name [] <| fun _ -> pristineCheck parsed
    cmd Release.Name [PristineCheck.Name; Build.Name] <| fun _ -> release parsed
    cmd Publish.Name [Release.Name] <| fun _ -> publish parsed
