open System.IO
open DanoHackApi.Example.DatasetProvider
open Giraffe
open Microsoft.AspNetCore.Builder
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Hosting
open Microsoft.Extensions.Logging

let webApp =
    GET >=>
        choose [
            route "/ping" >=> text "pong"
            route "/data" >=> fun next ctx ->
                text "" next ctx // Placeholder for actual data handling
        ]
   
let configureServices (services: IServiceCollection) =
    services
        .AddGiraffe()
        .AddLogging(fun b ->
            b.AddConsole()
             .SetMinimumLevel(LogLevel.Information) |> ignore)
        .AddSingleton<DatasetProviderService>() |> ignore
    
let configureApp (app: IApplicationBuilder) =
    app.UseGiraffe webApp

[<EntryPoint>]
let main args =
    let builder = WebApplication.CreateBuilder(args)
    configureServices builder.Services
    
    let app = builder.Build()
    configureApp app
    
    app.Run()

    0