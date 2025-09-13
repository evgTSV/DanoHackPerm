module MachineLearning

(*open System
open System.Linq
open System.Collections.Generic
open Microsoft.ML
open Microsoft.ML.Data
open Microsoft.ML.Trainers
open FSharp.Data
open Plotly.NET
open Plotly.NET.LayoutObjects
open Plotly.NET.TraceObjects
open Data

// Определяем поколения по возрасту клиента
let getGeneration age =
    match age with
    | age when age <= 26 -> "Z (Зумеры)"
    | age when age <= 41 -> "Y (Миллениалы)"
    | age when age <= 56 -> "X (Поколение X)"
    | _ -> "Бумеры"

// Создаем класс для данных ML
[<CLIMutable>]
type CustomerData = {
    [<LoadColumn(0)>] 
    CallId : string
    
    [<LoadColumn(1)>] 
    ClientId : string
    
    [<LoadColumn(2)>] 
    ClientAge : float32
    
    [<LoadColumn(3)>] 
    ClientGender : string
    
    [<LoadColumn(4)>] 
    ClientEducation : string
    
    [<LoadColumn(5)>] 
    UtilizationFlag : bool
    
    [<LoadColumn(6)>] 
    ApplicationFlag : bool
    
    [<LoadColumn(7)>] 
    VoiceOfferFlag : bool
    
    [<LoadColumn(8)>] 
    OfferTimeSec : float32
    
    [<LoadColumn(9)>] 
    NegativeFlag : bool
    
    [<LoadColumn(10)>] 
    CsatScore : Nullable<decimal>
    
    [<LoadColumn(11)>] 
    ClientAgreeFlg : int
    
    [<LoadColumn(12)>] 
    OfferType : string
    
    // Добавим вычисляемые поля
    Generation : string
    SusceptibilityScore : float32
}

// Функция для расчета скора восприимчивости
let calculateSusceptibilityScore (row: Provider.Row) =
    let score = 
        (if row.Utilization_flag then 1.0f else 0.0f) * 0.4f +
        (if row.Application_flag then 1.0f else 0.0f) * 0.3f +
        (if row.Voice_offer_flag then 1.0f else 0.0f) * 0.2f +
        (if row.Client_agree_flg = 1 then 1.0f else 0.0f) * 0.1f
    score

// Подготавливаем данные для ML
let mlData =
    data.Rows
    |> Seq.map (fun row ->
        let clientAge = 
            if row.Client_age.HasValue then float32 row.Client_age.Value 
            else 30.0f // среднее значение для пропущенных
        
        let generation = 
            if row.Client_age.HasValue then getGeneration row.Client_age.Value 
            else "Неизвестно"
            
        {
            CallId = string row.Call_id
            ClientId = string row.Client_id
            ClientAge = clientAge
            ClientGender = row.Client_gender
            ClientEducation = row.Client_education
            UtilizationFlag = row.Utilization_flag
            ApplicationFlag = row.Application_flag
            VoiceOfferFlag = row.Voice_offer_flag
            OfferTimeSec = 
                if row.Offer_time_sec.HasValue then float32 row.Offer_time_sec.Value 
                else 0.0f
            NegativeFlag = row.Negative_flg
            CsatScore = row.Csat_score
            ClientAgreeFlg = row.Client_agree_flg
            OfferType = row.Offer_type
            Generation = generation
            SusceptibilityScore = calculateSusceptibilityScore row
        })
    |> Seq.toArray

// Создаем ML контекст
let mlContext = MLContext(seed = 1)

// Загружаем данные в IDataView
let dataView = mlContext.Data.LoadFromEnumerable(mlData)

// Создаем конвейер обработки данных
let dataPipeline =
    EstimatorChain()
        .Append(mlContext.Transforms.Categorical.OneHotEncoding("ClientGenderEncoded", "ClientGender"))
        .Append(mlContext.Transforms.Categorical.OneHotEncoding("ClientEducationEncoded", "ClientEducation"))
        .Append(mlContext.Transforms.Categorical.OneHotEncoding("OfferTypeEncoded", "OfferType"))
        .Append(mlContext.Transforms.Concatenate("Features", 
            [| "ClientAge"; "UtilizationFlag"; "ApplicationFlag"; "VoiceOfferFlag"; 
               "OfferTimeSec"; "NegativeFlag"; "ClientAgreeFlg"; "ClientGenderEncoded"; 
               "ClientEducationEncoded"; "OfferTypeEncoded" |]))
        .Append(mlContext.Transforms.NormalizeMinMax("Features"))

// Обучаем конвейер и преобразуем данные
let transformer = dataPipeline.Fit(dataView)
let transformedData = transformer.Transform(dataView)

// Определяем параметры кластеризации K-means
let options = KMeansTrainer.Options()
options.NumberOfClusters <- 4
options.FeatureColumnName <- "Features"

// Создаем и обучаем модель кластеризации
let trainer = mlContext.Clustering.Trainers.KMeans(options)
let model = trainer.Fit(transformedData)

// Применяем модель к данным
let predictions = model.Transform(transformedData)

// Извлекаем предсказания кластеров
let clusterResults = 
    mlContext.Data.CreateEnumerable<ClusterPrediction>(predictions, reuseRowObject = false)
    |> Seq.toArray

// Добавляем кластеры к исходным данным
let dataWithClusters =
    Array.zip mlData clusterResults
    |> Array.map (fun (data, prediction) ->
        {| data with Cluster = prediction.PredictedLabel |})

// Анализируем кластеры
let clusterAnalysis =
    dataWithClusters
    |> Array.groupBy (fun x -> x.Cluster)
    |> Array.map (fun (cluster, items) ->
        let count = items.Length
        let avgAge = items |> Array.averageBy (fun x -> float x.ClientAge)
        let utilizationRate = items |> Array.averageBy (fun x -> if x.UtilizationFlag then 1.0 else 0.0) * 100.0
        let applicationRate = items |> Array.averageBy (fun x -> if x.ApplicationFlag then 1.0 else 0.0) * 100.0
        let voiceOfferRate = items |> Array.averageBy (fun x -> if x.VoiceOfferFlag then 1.0 else 0.0) * 100.0
        let susceptibilityScore = items |> Array.averageBy (fun x -> float x.SusceptibilityScore) * 100.0
        
        // Распределение по поколениям
        let generationDistribution =
            items
            |> Array.groupBy (fun x -> x.Generation)
            |> Array.map (fun (gen, genItems) -> 
                (gen, float genItems.Length / float count * 100.0))
            |> Map.ofArray
        
        {
            Cluster = cluster
            Count = count
            AvgAge = avgAge
            UtilizationRate = utilizationRate
            ApplicationRate = applicationRate
            VoiceOfferRate = voiceOfferRate
            SusceptibilityScore = susceptibilityScore
            GenerationDistribution = generationDistribution
        })
    |> Array.sortBy (fun x -> x.SusceptibilityScore)

// 1. График характеристик кластеров
let clusterCharacteristicsChart =
    let clusters = clusterAnalysis |> Array.map (fun x -> sprintf "Кластер %d" x.Cluster)
    
    let utilizationRates = clusterAnalysis |> Array.map (fun x -> x.UtilizationRate)
    let applicationRates = clusterAnalysis |> Array.map (fun x -> x.ApplicationRate)
    let voiceOfferRates = clusterAnalysis |> Array.map (fun x -> x.VoiceOfferRate)
    let susceptibilityScores = clusterAnalysis |> Array.map (fun x -> x.SusceptibilityScore)
    
    [
        Chart.Column(utilizationRates, clusters, Name = "Утилизация (%)")
        Chart.Column(applicationRates, clusters, Name = "Заявки (%)")
        Chart.Column(voiceOfferRates, clusters, Name = "Озвученные офферы (%)")
        Chart.Column(susceptibilityScores, clusters, Name = "Общий скор восприимчивости (%)")
    ]
    |> Chart.combine
    |> Chart.withTitle "Характеристики кластеров восприимчивости"
    |> Chart.withXAxisStyle "Кластер"
    |> Chart.withYAxisStyle "Процент"
    |> Chart.withLegend true
    |> Chart.withLayout(
        Layout() 
        |> Layout.setLayoutStyle(
            LayoutStyle(
                Barmode = StyleParam.BarMode.Group
            )
        )
    )

// 2. Распределение поколений по кластерам
let generationClusterChart =
    let clusters = clusterAnalysis |> Array.map (fun x -> sprintf "Кластер %d" x.Cluster)
    
    let generations = 
        clusterAnalysis 
        |> Array.collect (fun x -> 
            x.GenerationDistribution 
            |> Map.toArray 
            |> Array.map fst)
        |> Array.distinct
        |> Array.sort
    
    let data =
        generations
        |> Array.map (fun gen ->
            let values = 
                clusterAnalysis
                |> Array.map (fun x ->
                    match x.GenerationDistribution.TryFind gen with
                    | Some value -> value
                    | None -> 0.0
                )
            Chart.Column(values, clusters, Name = gen)
        )
        |> Array.toList
    
    data
    |> Chart.combine
    |> Chart.withTitle "Распределение поколений по кластерам восприимчивости"
    |> Chart.withXAxisStyle "Кластер"
    |> Chart.withYAxisStyle "Процент"
    |> Chart.withLegend true
    |> Chart.withLayout(
        Layout() 
        |> Layout.setLayoutStyle(
            LayoutStyle(
                Barmode = StyleParam.BarMode.Stack
            )
        )
    )

// 3. Scatter plot кластеров по возрасту и восприимчивости
let clusterScatterPlot =
    let clusterColors = [| "#1f77b4"; "#ff7f0e"; "#2ca02c"; "#d62728" |]
    
    clusterAnalysis
    |> Array.mapi (fun i cluster ->
        let clusterData = 
            dataWithClusters 
            |> Array.filter (fun x -> x.Cluster = cluster.Cluster)
        
        let ages = clusterData |> Array.map (fun x -> float x.ClientAge)
        let susceptibilityScores = clusterData |> Array.map (fun x -> float x.SusceptibilityScore * 100.0)
        
        Chart.Scatter(
            ages, susceptibilityScores,
            Mode = StyleParam.Mode.Markers,
            Name = sprintf "Кластер %d" cluster.Cluster,
            Marker = Marker.init(
                Size = 10,
                Color = Color.fromHex clusterColors[i],
                Opacity = 0.6
            )
        )
    )
    |> Chart.combine
    |> Chart.withTitle "Распределение кластеров по возрасту и восприимчивости"
    |> Chart.withXAxisStyle "Возраст клиента"
    |> Chart.withYAxisStyle "Скор восприимчивости (%)"
    |> Chart.withLegend true

// 4. Heatmap корреляции между характеристиками кластеров
let clusterHeatmap =
    let characteristics = [|
        "Средний возраст", clusterAnalysis |> Array.map (fun x -> x.AvgAge)
        "Утилизация", clusterAnalysis |> Array.map (fun x -> x.UtilizationRate)
        "Заявки", clusterAnalysis |> Array.map (fun x -> x.ApplicationRate)
        "Озвученные офферы", clusterAnalysis |> Array.map (fun x -> x.VoiceOfferRate)
        "Восприимчивость", clusterAnalysis |> Array.map (fun x -> x.SusceptibilityScore)
    |]
    
    let clusterLabels = clusterAnalysis |> Array.map (fun x -> sprintf "Кластер %d" x.Cluster)
    let characteristicLabels = characteristics |> Array.map fst
    
    let heatmapData =
        characteristics
        |> Array.map (fun (_, values) -> values)
        |> Array.transpose
        |> Array.map Array.ofSeq
    
    Chart.Heatmap(
        heatmapData,
        X = characteristicLabels,
        Y = clusterLabels,
        Colorscale = StyleParam.Colorscale.Viridis,
        ShowScale = true
    )
    |> Chart.withTitle "Тепловая карта характеристик кластеров"
    |> Chart.withXAxisStyle "Характеристика"
    |> Chart.withYAxisStyle "Кластер"

// 5. Анализ наиболее эффективных офферов для каждого кластера
let clusterOfferAnalysis =
    let clusterOfferData =
        dataWithClusters
        |> Array.groupBy (fun x -> (x.Cluster, x.OfferType))
        |> Array.map (fun ((cluster, offerType), items) ->
            let count = items.Length
            let successRate = 
                items 
                |> Array.averageBy (fun x -> 
                    if x.UtilizationFlag || x.ApplicationFlag then 1.0 else 0.0) * 100.0
            (cluster, offerType, count, successRate))
        |> Array.toList
    
    let clusters = clusterOfferData |> List.map (fun (c, _, _, _) -> c) |> List.distinct |> List.sort
    let offerTypes = clusterOfferData |> List.map (fun (_, o, _, _) -> o) |> List.distinct |> List.sort
    
    let heatmapData =
        clusters
        |> List.map (fun cluster ->
            offerTypes
            |> List.map (fun offerType ->
                match clusterOfferData |> List.tryFind (fun (c, o, _, _) -> c = cluster && o = offerType) with
                | Some (_, _, _, rate) -> rate
                | None -> 0.0
            )
        )
    
    Chart.Heatmap(
        heatmapData,
        X = offerTypes,
        Y = clusters |> List.map (sprintf "Кластер %d"),
        Colorscale = StyleParam.Colorscale.Viridis,
        ShowScale = true
    )
    |> Chart.withTitle "Эффективность типов офферов по кластерам (%)"
    |> Chart.withXAxisStyle "Тип оффера"
    |> Chart.withYAxisStyle "Кластер"

// Создаем dashboard с результатами кластеризации
let clusteringDashboard =
    [
        clusterCharacteristicsChart
        generationClusterChart
        clusterScatterPlot
        clusterHeatmap
        clusterOfferAnalysis
    ]
    |> Chart.Grid(3, 2)
    |> Chart.withSize(1600, 1200)

// Показываем dashboard
clusteringDashboard |> Chart.show

// Выводим статистику по кластерам
printfn "Результаты кластеризации клиентов по восприимчивости:"
printfn "Кластер\tКоличество\tСред. возраст\tУтилизация\tЗаявки\tВосприимчивость"
clusterAnalysis
|> Array.iter (fun cluster ->
    printfn $"%d{cluster.Cluster}\t%d{cluster.Count}\t%.1f{cluster.AvgAge}\t%.1f%%\t%.1f%%\t%.1f%%" 
        cluster.UtilizationRate cluster.ApplicationRate cluster.SusceptibilityScore
)

// Анализ лучших и худших кластеров
let bestCluster = clusterAnalysis |> Array.maxBy (fun x -> x.SusceptibilityScore)
let worstCluster = clusterAnalysis |> Array.minBy (fun x -> x.SusceptibilityScore)

printfn $"\nСамый восприимчивый кластер: {bestCluster.Cluster}"
printfn $"Самый невосприимчивый кластер: {worstCluster.Cluster}"
printfn $"Разница в восприимчивости: {bestCluster.SusceptibilityScore - worstCluster.SusceptibilityScore:.1f}%"

// Рекомендации на основе анализа кластеров
printfn "\nРекомендации по кластерам:"
clusterAnalysis
|> Array.iter (fun cluster ->
    let topGeneration = 
        cluster.GenerationDistribution 
        |> Map.toArray 
        |> Array.maxBy snd 
        |> fst
    
    printfn $"Кластер {cluster.Cluster} ({topGeneration}):"
    printfn $"  - Средний возраст: {cluster.AvgAge:.1f}"
    printfn $"  - Восприимчивость: {cluster.SusceptibilityScore:.1f}%"
    printfn $"  - Рекомендуемые офферы: {getTopOffersForCluster cluster.Cluster clusterOfferData}"
)

// Функция для получения топовых офферов для кластера
let getTopOffersForCluster clusterId clusterData =
    clusterData
    |> List.filter (fun (c, _, _, _) -> c = clusterId)
    |> List.sortByDescending (fun (_, _, _, rate) -> rate)
    |> List.take 3
    |> List.map (fun (_, offer, _, rate) -> sprintf "%s (%.1f%%)" offer rate)
    |> String.concat ", "*)