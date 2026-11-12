module Program

open System
open System.IO
open System.Runtime.InteropServices
open Nullean.Make.Fs
open Proc.Fs

/// The build's targets; `./build.sh --help` lists them with these descriptions. Make.Fs turns a case name into
/// kebab-case, so the multi-word ones are spelled as one word: CI calls `generatepackages`, `publishcontainers`, ...
type Target =
    /// clean known output locations
    | Clean
    /// Run build
    | Build
    /// Runs build, then creates and validates the packages and reports public API changes
    | Pack
    /// Runs build, then creates and validates the packages: what CI's build job runs
    | Ci
    /// Pack the root and 'any' tool packages into build/output; CI's aot-pack matrix builds the per-RID ones
    | Generatepackages
    /// Validate the signed managed assembly in the 'any' package
    | Validatepackages
    /// Report public API changes against the previous release
    | Generateapichanges
    /// Publish the native-AOT build as the ghcr.io/nullean/release-notes container image
    | Publishcontainers
    /// Publish the native-AOT build for this machine and read a public repository's releases and issues with it
    | Smokeaot

/// build/output, relative to the repository root
let private output = Paths.RootRelative Paths.Output.FullName

let private toolProject = Paths.RootRelative Paths.ToolProject.FullName

[<EntryPoint>]
let main argv =
    let app = MakeApp<Target>("./build.sh", None)
    let push =
        app.Flag("--push", desc = "publishcontainers only: push the built image to ghcr.io instead of building it into the local Docker daemon")

    // `-s` skips what a target `requires`, never what it `composes`: build always cleans first
    app.Bind <| function
        | Clean -> target [] {
              if Paths.Output.Exists then Paths.Output.Delete true
              // clean the tool project but not this build itself: `dotnet clean` on the solution would delete the
              // assemblies the running build still loads lazily and fail the next target
              for project in Directory.EnumerateFiles("src", "*.?sproj", SearchOption.AllDirectories) do
                  sh $"dotnet clean {project} -c Release -v q" }
        | Build -> command {
              composes [Clean]
              executes (shell { sh $"dotnet build -c Release" }) }
        // there are no tests, so ci is pack: build, then create, validate and diff the packages
        | Pack -> command { requires [Build]; composes [Generatepackages; Validatepackages; Generateapichanges] }
        | Ci -> command { requires [Pack]; composes [] }
        | Generatepackages -> target [] {
              if not Paths.Output.Exists then Paths.Output.Create()
              // A plain `dotnet pack` emits the root package (whose DotnetToolSettings.xml v2 maps each RID to
              // its own package) AND a package per RID — but native AOT can only compile for the machine it
              // runs on, so those per-RID outputs from a single machine are self-contained MANAGED builds,
              // silently missing the AOT compilation. We therefore keep only the root and the portable 'any'
              // fallback here, and take the real per-RID packages from the CI matrix, where each is compiled
              // on a matching runner (see aot-pack in .github/workflows/ci.yml).
              let staging = Paths.RootRelative <| Path.Combine(Paths.Output.FullName, "..", "release-notes-staging")
              if Directory.Exists staging then Directory.Delete(staging, true)
              sh $"dotnet pack {toolProject} -c Release -o {staging}"
              for package in DirectoryInfo(staging).GetFiles("*.nupkg") do
                  if not (Packaging.isPerRidPackage package.Name) then
                      let destination = Path.Combine(Paths.Output.FullName, package.Name)
                      printfn "keeping %s" package.Name
                      package.CopyTo(destination, true) |> ignore
                      if package.Name.Contains(sprintf "%s.any." Paths.ToolName) then
                          Packaging.fixAnyPackageSigning destination
              Directory.Delete(staging, true) }
        | Validatepackages -> target [] {
              // Only the 'any' package carries a signed managed assembly to check: the root package is
              // just a DotnetToolSettings.xml pointer with no dll of its own, and the per-RID AOT packages
              // hold a native binary with no managed identity either.
              let package =
                  Paths.Output.GetFiles "*.nupkg"
                  |> Seq.filter (fun f -> f.Name.Contains(sprintf "%s.any." Paths.ToolName))
                  |> Seq.sortByDescending (fun f -> f.CreationTimeUtc)
                  |> Seq.head
                  |> fun p -> Paths.RootRelative p.FullName
              let version = Versions.informational.Value
              sh $"dotnet nupkg-validator validate {package} -v {version} -a {Paths.ToolName} -k 96c599bbe3e70f5d" }
        | Generateapichanges -> target [] {
              let version = Versions.current.Value
              let tool = Paths.ToolName
              // The plain "release-notes" package id is just a DotnetToolSettings.xml v2 shim pointing
              // at per-RID sub-packages (see Generatepackages) - it ships no managed assembly of its own,
              // so NuGetAssemblyProvider would find 0 assemblies there. The portable, signed managed build
              // lives in "release-notes.any" instead.
              // The provider specs are quoted: an unquoted | would pipe.
              sh $"dotnet assembly-differ 'previous-nuget|{tool}.any|{version}|net10.0' 'directory|src/{tool}/bin/Release/net10.0' --target {tool} -f github-comment --output {output}" }
        // Publishes the CLI's native-AOT build as a container image via the .NET SDK's own container
        // support (`dotnet publish -t:PublishContainer`), the same mechanism used for curb — see
        // https://github.com/nullean/curb/pull/72. linux-x64 only for now; a second RID becomes a second
        // manifest-list platform later with no change to action.yml.
        //
        // Base image is the chiseled/distroless runtime-deps image: no shell, minimal surface, and correct
        // for an AOT binary specifically because there is no managed runtime to host — a plain `runtime`
        // image would carry a CLR this binary never uses.
        //
        // --push is an explicit flag, not inferred from a CI/event-name environment variable: the aot-pack
        // job's linux-x64 leg calls this on every trigger purely to prove the container build itself still
        // works, with no ghcr.io credentials configured there. Only the build job, which does log in, passes
        // --push.
        | Publishcontainers -> target [] {
              let baseImage = "mcr.microsoft.com/dotnet/runtime-deps:10.0-noble-chiseled"
              // MSBuild reads the quotes: without them the tags' `;` would split the property
              let tags = $"ContainerImageTags=\"{Packaging.containerImageTags Versions.current.Value}\""
              if push.Value then
                  sh $"dotnet publish {toolProject} -c Release -f net10.0 -r linux-x64 /t:PublishContainer -p DebugType=none -p ContainerBaseImage={baseImage} -p ContainerRepository={Paths.Repository} -p {tags} -p ContainerUser=1001:1001 -p ContainerRegistry=ghcr.io"
              else
                  sh $"dotnet publish {toolProject} -c Release -f net10.0 -r linux-x64 /t:PublishContainer -p DebugType=none -p ContainerBaseImage={baseImage} -p ContainerRepository={Paths.Repository} -p {tags} -p ContainerUser=1001:1001" }

        // The framework-dependent build cannot catch what trimming breaks: #47 broke only the native-AOT binary, where
        // Octokit's reflection-based JSON returned nothing or threw. So this publishes that binary and reads releases
        // and issues that no longer change: elastic/apm-agent-dotnet's 1.11.0 and the line around it. It runs
        // anonymously, as the issue's reproduction did, which costs about 15 of the 60 requests an hour GitHub allows.
        | Smokeaot -> target [] {
              let rid = RuntimeInformation.RuntimeIdentifier
              let publishDirectory = Path.Combine(Paths.Output.FullName, "aot-smoke")
              let publish = Paths.RootRelative publishDirectory
              sh $"dotnet publish {toolProject} -c Release -f net10.0 -r {rid} -o {publish}"
              let executable = if OperatingSystem.IsWindows() then $"{Paths.ToolName}.exe" else Paths.ToolName
              let binary = Paths.RootRelative <| Path.Combine(publishDirectory, executable)
              // checked one command at a time and stopping at the first failure, so a broken binary spends few requests
              let check (command: string) (result: string) (expected: string list) =
                  printfn "%s %s:\n%s" Paths.ToolName command result
                  for line in expected do
                      if not (result.Contains line) then failwithf "the native-AOT binary's %s did not print '%s'" command line
              // the latest release, then every page of releases: one object, then lists
              let! previous = text $"{binary} find-previous elastic apm-agent-dotnet --version 1.11.0"
              check "find-previous" previous [ "1.10.0" ]
              // every page of releases again, then one release by its tag
              let! current = text $"{binary} current-version elastic apm-agent-dotnet --query 1.11 --version 1.11.0"
              check "current-version" current [ "1.11.1"; "1.11.2" ]
              // issues and pull requests, with their labels and pull request links
              let! notes = text $"{binary} generate elastic apm-agent-dotnet --version 1.11.0"
              check "generate" notes [ "- #1239 Add Cosmos DB support  (issue: #1154)"; "- #1302 Prefer W3C traceparent" ] }

    app.RunAsync(argv).GetAwaiter().GetResult()
