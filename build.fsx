#load ".fake/build.fsx/intellisense.fsx"

// ========================================================================================================
// === F# / Application fake build ================================================================ 2.0.1 =
// --------------------------------------------------------------------------------------------------------
// Options:
//  - no-clean   - disables clean of dirs in the first step (required on CI)
//  - no-lint    - lint will be executed, but the result is not validated
// --------------------------------------------------------------------------------------------------------
// Table of contents:
//      1. Information about project, configuration
//      2. Utilities, DotnetCore functions
//      3. FAKE targets
//      4. FAKE targets hierarchy
// ========================================================================================================

open System
open System.IO

open Fake.Core
open Fake.DotNet
open Fake.IO
open Fake.IO.FileSystemOperators
open Fake.IO.Globbing.Operators
open Fake.Tools.Git

// --------------------------------------------------------------------------------------------------------
// 1. Information about the project to be used at NuGet and in AssemblyInfo files and other FAKE configuration
// --------------------------------------------------------------------------------------------------------

let project = "Kafka example"
let summary = "Example for kafka with F# in docker"
let releaseDir = "/app"

let changeLog = None
let gitCommit = Information.getCurrentSHA1(".")
let gitBranch = Information.getBranchName(".")

[<RequireQualifiedAccess>]
module ProjectSources =
    let release =
        !! "./*.fsproj"
        ++ "src/**/*.fsproj"

    let tests =
        !! "tests/**/*.fsproj"

    let all =
        release
        ++ "tests/**/*.fsproj"

// --------------------------------------------------------------------------------------------------------
// 2. Utilities, DotnetCore functions, etc.
// --------------------------------------------------------------------------------------------------------

[<AutoOpen>]
module private Utils =
    let tee f a =
        f a
        a

    let skipOn option action p =
        if p.Context.Arguments |> Seq.contains option
        then Trace.tracefn "Skipped ..."
        else action p

    let createProcess exe arg dir =
        CreateProcess.fromRawCommandLine exe arg
        |> CreateProcess.withWorkingDirectory dir
        |> CreateProcess.ensureExitCode

    let run proc arg dir =
        proc arg dir
        |> Proc.run
        |> ignore

    let orFail = function
        | Error e -> raise e
        | Ok ok -> ok

    let envVar name =
        if Environment.hasEnvironVar(name)
            then Environment.environVar(name) |> Some
            else None

    let stringToOption = function
        | null | "" -> None
        | string -> Some string

    [<RequireQualifiedAccess>]
    module Option =
        let mapNone f = function
            | Some v -> v
            | None -> f None

        let bindNone f = function
            | Some v -> Some v
            | None -> f None

[<RequireQualifiedAccess>]
module Dotnet =
    let dotnet = createProcess "dotnet"

    let run command dir = try run dotnet command dir |> Ok with e -> Error e
    let runInRoot command = run command "."
    let runOrFail command dir = run command dir |> orFail
    let runInRootOrFail command = run command "." |> orFail

// --------------------------------------------------------------------------------------------------------
// 3. Targets for FAKE
// --------------------------------------------------------------------------------------------------------

Target.create "Clean" <| skipOn "no-clean" (fun _ ->
    !! "./**/bin/Release"
    ++ "./**/bin/Debug"
    ++ "./**/obj"
    ++ "./**/.ionide"
    |> Shell.cleanDirs
)

Target.create "AssemblyInfo" (fun _ ->
    let getAssemblyInfoAttributes projectName =
        let now = DateTime.Now

        let release =
            changeLog
            |> Option.bind (fun changeLog ->
                try ReleaseNotes.parse (File.ReadAllLines changeLog |> Seq.filter ((<>) "## Unreleased")) |> Some
                with _ -> None
            )

        let gitValue fallbackEnvironmentVariableNames initialValue =
            initialValue
            |> String.replace "NoBranch" ""
            |> stringToOption
            |> Option.bindNone (fun _ -> fallbackEnvironmentVariableNames |> List.tryPick envVar)
            |> Option.defaultValue "unknown"

        [
            AssemblyInfo.Title projectName
            AssemblyInfo.Product project
            AssemblyInfo.Description summary

            match release with
            | Some release ->
                AssemblyInfo.Version release.AssemblyVersion
                AssemblyInfo.FileVersion release.AssemblyVersion
            | _ ->
                AssemblyInfo.Version "1.0"
                AssemblyInfo.FileVersion "1.0"

            AssemblyInfo.InternalsVisibleTo "tests"
            AssemblyInfo.Metadata("gitbranch", gitBranch |> gitValue [ "GIT_BRANCH"; "branch" ])
            AssemblyInfo.Metadata("gitcommit", gitCommit |> gitValue [ "GIT_COMMIT"; "commit" ])
            AssemblyInfo.Metadata("buildNumber", "BUILD_NUMBER" |> envVar |> Option.defaultValue "-")
            AssemblyInfo.Metadata("createdAt", now.ToString("yyyy-MM-dd HH:mm:ss"))
        ]

    let getProjectDetails (projectPath: string) =
        let projectName = Path.GetFileNameWithoutExtension(projectPath)
        (
            projectPath,
            projectName,
            Path.GetDirectoryName(projectPath),
            (getAssemblyInfoAttributes projectName)
        )

    ProjectSources.all
    |> Seq.map getProjectDetails
    |> Seq.iter (fun (_, _, folderName, attributes) ->
        AssemblyInfoFile.createFSharp (folderName </> "AssemblyInfo.fs") attributes
    )
)

Target.create "Build" (fun _ ->
    ProjectSources.all
    |> Seq.iter (DotNet.build id)
)

Target.create "Lint" <| skipOn "no-lint" (fun _ ->
    ProjectSources.all
    ++ "./Build.fsproj"
    |> Seq.iter (fun fsproj ->
        match Dotnet.runInRoot (sprintf "fsharplint lint %s" fsproj) with
        | Ok () -> Trace.tracefn "Lint %s is Ok" fsproj
        | Error e -> raise e
    )
)

Target.create "Tests" (fun _ ->
    if ProjectSources.tests |> Seq.isEmpty
    then Trace.tracefn "There are no tests yet."
    else Dotnet.runOrFail "run" "tests"
)

Target.create "Release" (fun _ ->
    releaseDir
    |> sprintf "publish -c Release -o %s"
    |> Dotnet.runInRootOrFail
)

Target.create "Watch" (fun _ ->
    Dotnet.runInRootOrFail "watch run"
)

Target.create "Run" (fun _ ->
    Dotnet.runInRootOrFail "run"
)

// --------------------------------------------------------------------------------------------------------
// 4. FAKE targets hierarchy
// --------------------------------------------------------------------------------------------------------

open Fake.Core.TargetOperators

"Clean"
    ==> "AssemblyInfo"
    ==> "Build"
    ==> "Lint"
    ==> "Tests"
    ==> "Release" <=> "Watch" <=> "Run"

Target.runOrDefaultWithArguments "Build"
