module CommandLine

open Argu

type BumpArguments =
    | [<First; CliPrefix(CliPrefix.None)>] Version of string
    with
    interface IArgParserTemplate with
        member s.Usage =
            match s with
            | Version _ -> "Optionally set the new version to bump to, otherwise increments the patch version"
    static member Name = "bump"
and Arguments =
    | [<CliPrefix(CliPrefix.None);SubCommand>] Bump of ParseResults<BumpArguments>
    | [<CliPrefix(CliPrefix.None);SubCommand>] PristineCheck 
    | [<CliPrefix(CliPrefix.None);SubCommand>] Build 
    | [<CliPrefix(CliPrefix.None);SubCommand>] Release 
    | [<CliPrefix(CliPrefix.None);SubCommand>] Publish
    
    | [<Inherit;AltCommandLine("-s")>] SingleTarget of bool
with
    interface IArgParserTemplate with
        member this.Usage =
            match this with
            | Bump _ -> "bump the version, create a bump commit and push"
            | Build _ -> "Run build and tests"
            | PristineCheck _ -> "validates the repository is in a clean state"
            | Release _ -> "runs build, and create an validates the packages shy of publishing them"
            | Publish _ -> "Runs the release command, tags the current commit and pushes the tag"
            
            | SingleTarget _ -> "Runs the provided sub command without running their dependencies"
    member this.Name =
        match this with 
        | Bump _ -> "bump"
        | PristineCheck -> "pristinecheck"
        | Build -> "build"
        | Release -> "release"
        | Publish -> "publish"
        | x -> failwithf "Not a subcommand %A" x
