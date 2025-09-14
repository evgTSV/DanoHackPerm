module DanoHackAnalysis.MLVisualize

open System
open Microsoft.ML
open Microsoft.ML.Data
open Plotly.NET
open Plotly.NET.TraceObjects
open Plotly.NET.LayoutObjects
open MLAnalysis

let visualizeClusters (mlContext: MLContext) (model: ITransformer)=
    // Прогоняем данные через модель
    let engine = mlContext.Model.CreatePredictionEngine<InputData, ClusterPrediction>(model)
    let predictions =
        inputData
        |> Seq.map (fun row ->
            let pred = engine.Predict(row)
            (float row.client_age, float row.csat_score, int pred.ClusterId))
        |> Seq.toList

    // Разделяем по кластерам
    let grouped =
        predictions
        |> List.groupBy (fun (_,_,cluster) -> cluster)
        |> List.sortBy fst

    // Рисуем scatter для каждого кластера
    let charts =
        grouped
        |> List.map (fun (cluster, pts) ->
            let x = pts |> List.map (fun (age,_,_) -> age)
            let y = pts |> List.map (fun (_,csat,_) -> csat)
            Chart.Scatter(x, y,
                          mode = StyleParam.Mode.Markers,
                          Name = sprintf "Кластер %d" cluster,
                          Marker = Marker.init(Size=10, Opacity=0.7)))
    
    charts
    |> Chart.combine
    |> Chart.withTitle "Кластеризация клиентов (возраст vs CSAT)"
    |> Chart.withXAxisStyle "Возраст клиента"
    |> Chart.withYAxisStyle "CSAT"
    
let show _ =
    let mlContext = MLContext(seed = Nullable 42)
    let model = runClustering()
    visualizeClusters mlContext model
    |> Chart.show