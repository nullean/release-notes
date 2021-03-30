namespace ReleaseNotes

open System
open System.IO
open System.Text

type OutputWriter(output: string option) =
    let stdout = Console.Out
    let sb = new StringBuilder()
    do
        output |> Option.iter (fun o ->
            printfn ""
            printfn "Building %s" o
            printfn "-------------------"   
        )
    
    member this.EmptyLine () =
        stdout.WriteLine()
        sb.AppendLine() |> ignore
    member this.WriteLine (s:string) =
        stdout.WriteLine(s)
        sb.AppendLine(s) |> ignore
    override this.ToString () = sb.ToString()
    interface IDisposable with 
        member __.Dispose() =
            stdout.Dispose()
            output |> Option.iter (fun f -> File.WriteAllText(f, sb.ToString()))
        
