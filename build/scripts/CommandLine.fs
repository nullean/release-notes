module CommandLine

open Argu
open Microsoft.FSharp.Reflection

type Arguments =
    | [<CliPrefix(CliPrefix.None);SubCommand>] Clean
    | [<CliPrefix(CliPrefix.None);SubCommand>] Build
    
    | [<CliPrefix(CliPrefix.None);Hidden;SubCommand>] PristineCheck 
    | [<CliPrefix(CliPrefix.None);Hidden;SubCommand>] GeneratePackages
    | [<CliPrefix(CliPrefix.None);Hidden;SubCommand>] ValidatePackages 
    | [<CliPrefix(CliPrefix.None);Hidden;SubCommand>] GenerateReleaseNotes 
    | [<CliPrefix(CliPrefix.None);SubCommand>] Release
    
    | [<CliPrefix(CliPrefix.None);Hidden;SubCommand>] PublishRelease 
    | [<CliPrefix(CliPrefix.None);Hidden;SubCommand>] PublishNewLabels 
    | [<CliPrefix(CliPrefix.None);SubCommand>] Publish
    
    | [<Inherit;AltCommandLine("-s")>] SingleTarget of bool
with
    interface IArgParserTemplate with
        member this.Usage =
            match this with
            | Clean _ -> "clean known output locations"
            | Build _ -> "Run build and tests"
            | Release _ -> "runs build, and create an validates the packages shy of publishing them"
            | Publish _ -> "Runs the full release"
            
            | SingleTarget _ -> "Runs the provided sub command without running their dependencies"
            
            | PristineCheck  
            | GeneratePackages
            | ValidatePackages 
            | GenerateReleaseNotes
            | PublishRelease 
            | PublishNewLabels
                -> "Undocumented, dependent target"
    member this.Name =
        match FSharpValue.GetUnionFields(this, typeof<Arguments>) with
        | case, _ -> case.Name.ToLowerInvariant()
