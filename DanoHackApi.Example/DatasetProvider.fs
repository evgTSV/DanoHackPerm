module DanoHackApi.Example.DatasetProvider

open System
open System.IO
open System.Threading.Tasks
open Deedle
open FSharp.Data
open FSharp.Data.Runtime
open Microsoft.Extensions.Logging

[<Literal>]
let ResolutionFolder = __SOURCE_DIRECTORY__

[<Literal>]
let DataFileName = "data.csv"

type Provider = CsvProvider<DataFileName, ResolutionFolder = ResolutionFolder>

type DatasetProviderService(
    logger: ILogger<DatasetProviderService>) =
    
    member _.createFromFile (loader: string -> 'a Task)=
        task {
            try
                return! loader <| Path.Combine(ResolutionFolder, DataFileName)
            with
            | ex ->
                logger.LogError($"Error loading data: {ex.Message}")
                return failwith "Failed to load dataset"
        }
    
    member this.getDataCsv _ =
        this.createFromFile (fun path -> task {
            return! Provider.AsyncLoad(path)
        })
        
    member this.getDataFrame _=
        this.createFromFile (fun path -> task {
            return Frame.ReadCsv(path, hasHeaders=true, separators=",")
        })