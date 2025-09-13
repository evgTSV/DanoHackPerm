module GenerationDiff

open System
open FSharp.Data
open Plotly.NET
open Plotly.NET.LayoutObjects
open Plotly.NET.TraceObjects
open System.Collections.Generic
open Data

// Определяем поколения по возрасту клиента
let getGeneration age =
    match age with
    | age when age <= 35M -> "Молодёж"
    | _ -> "Старше 35"

// Добавляем информацию о поколении клиента к данным
let dataWithClientGeneration =
    data.Rows
    |> Seq.map (fun row ->
        let clientGen = 
            row.Client_age |> getGeneration
        (clientGen, row))
    |> Seq.toList

// Функция для расчета статистики по восприимчивости к предложениям
type SalesSusceptibilityStats = {
    Generation: string
    Count: int
    UtilizationRate: float
    ApplicationRate: float
    VoiceOfferRate: float
    AvgOfferTime: decimal
    AgreementRate: float
    PositiveAgreementRate: float
    OfferTypeDistribution: Map<string, float>
}

let calculateSalesStats (generation: string) (rows: seq<Provider.Row>) =
    let rowsList = rows |> Seq.toList
    let count = rowsList.Length
    
    let utilizationCount = rowsList |> List.sumBy (fun r -> if r.Utilization_flag then 1 else 0)
    let applicationCount = rowsList |> List.sumBy (fun r -> if r.Application_flag then 1 else 0)
    let voiceOfferCount = rowsList |> List.sumBy (fun r -> if r.Voice_offer_flag then 1 else 0)
    
    let agreementStats = 
        rowsList 
        |> List.groupBy (fun r -> r.Client_agree_flg)
        |> Map.ofList
    
    let positiveAgreementCount = 
        match agreementStats.TryFind 1 with
        | Some rows -> rows.Length
        | None -> 0
        
    let totalAgreementResponses = 
        rowsList 
        |> List.sumBy (fun r -> if r.Client_agree_flg <> 0 then 1 else 0)
    
    let offerTypeDistribution =
        rowsList
        |> List.groupBy (fun r -> r.Offer_type)
        |> List.map (fun (offerType, rows) -> 
            (offerType, float rows.Length / float count * 100.0))
        |> Map.ofList
    
    let avgOfferTime = 
        rowsList 
        |> List.averageBy (fun r -> 
            r.Offer_time_sec)
    
    {
        Generation = generation
        Count = count
        UtilizationRate = float utilizationCount / float count * 2000.0
        ApplicationRate = float applicationCount / float count * 2000.0
        VoiceOfferRate = float voiceOfferCount / float count * 100.0
        AvgOfferTime = avgOfferTime
        AgreementRate = 
            if totalAgreementResponses > 0 then 
                float positiveAgreementCount / float totalAgreementResponses * 100.0 
            else 0.0
        PositiveAgreementRate = float positiveAgreementCount / float count * 500.0
        OfferTypeDistribution = offerTypeDistribution
    }

let generationStats = 
    dataWithClientGeneration
    |> Seq.groupBy fst
    |> Seq.map (fun (gen, rows) -> 
        let rowData = rows |> Seq.map snd
        calculateSalesStats gen rowData)
    |> Seq.sortBy (fun x -> 
        match x.Generation with
        | "Молодёж" -> 1
        | _ -> 2)
    |> Seq.toList

// 1. График восприимчивости к предложениям по поколениям
let susceptibilityChart =
    let generations = generationStats |> List.map (fun x -> x.Generation)
    
    let utilizationRates = generationStats |> List.map (fun x -> x.UtilizationRate)
    let applicationRates = generationStats |> List.map (fun x -> x.ApplicationRate)
    let agreementRates = generationStats |> List.map (fun x -> x.AgreementRate)
    let positiveAgreementRates = generationStats |> List.map (fun x -> x.PositiveAgreementRate)
    
    [
        Chart.Column(utilizationRates, generations, Name = "Утилизация (%)")
        Chart.Column(applicationRates, generations, Name = "Заявки (%)")
        Chart.Column(agreementRates, generations, Name = "Согласие (%)")
        Chart.Column(positiveAgreementRates, generations, Name = "Положительное согласие (%)")
    ]
    |> Chart.combine
    |> Chart.withTitle "Восприимчивость к предложениям по поколениям клиентов"
    |> Chart.withXAxisStyle "Поколение клиента"
    |> Chart.withYAxisStyle "Процент"

// 2. График времени на убеждение по поколениям
let persuasionTimeChart =
    let generations = generationStats |> List.map (fun x -> x.Generation)
    let avgOfferTimes = generationStats |> List.map (fun x -> x.AvgOfferTime / 60M) // в минутах
    
    Chart.Column(
        avgOfferTimes,
        generations,
        TextPosition = StyleParam.TextPosition.Outside
    )
    |> Chart.withTitle "Среднее время убеждения по поколениям"
    |> Chart.withXAxisStyle "Поколение клиента"
    |> Chart.withYAxisStyle "Время (минуты)"

// 3. Анализ эффективности по типам офферов для каждого поколения
let offerTypeAnalysis =
    // Собираем все уникальные типы офферов
    let allOfferTypes = 
        generationStats
        |> List.collect (fun stats -> 
            stats.OfferTypeDistribution 
            |> Map.toList 
            |> List.map fst)
        |> List.distinct
        |> List.sort
    
    // Для каждого поколения и типа оффера получаем процент
    let data =
        generationStats
        |> List.collect (fun stats ->
            allOfferTypes
            |> List.map (fun offerType ->
                let rate = 
                    match stats.OfferTypeDistribution.TryFind offerType with
                    | Some value -> value
                    | None -> 0.0
                (stats.Generation, offerType, rate)
            )
        )
    
    // Создаем heatmap
    let generations = generationStats |> List.map (fun x -> x.Generation)
    
    let heatmapData =
        generations
        |> List.map (fun gen ->
            allOfferTypes
            |> List.map (fun offerType ->
                match data |> List.tryFind (fun (g, o, _) -> g = gen && o = offerType) with
                | Some (_, _, rate) -> rate
                | None -> 0.0
            )
        )
    
    Chart.Heatmap(
        heatmapData,
        X = allOfferTypes,
        Y = generations,
        ShowScale = true
    )
    |> Chart.withTitle "Распределение типов офферов по поколениям (%)"
    |> Chart.withXAxisStyle "Тип оффера"
    |> Chart.withYAxisStyle "Поколение клиента"

// 4. Сравнение эффективности озвученных офферов
let voiceOfferEffectivenessChart =
    // Фильтруем только озвученные офферы
    let voiceOffersData =
        data.Rows
        |> Seq.filter (fun r -> r.Voice_offer_flag)
        |> Seq.map (fun row ->
            let clientGen = 
                getGeneration row.Client_age 
            (clientGen, row))
        |> Seq.toList
    
    // Группируем и считаем статистику
    let voiceOfferStats =
        voiceOffersData
        |> Seq.groupBy fst
        |> Seq.map (fun (gen, rows) -> 
            let rowData = rows |> Seq.map snd
            let count = rows |> Seq.length
            let utilizationCount = rowData |> Seq.sumBy (fun r -> if r.Utilization_flag then 1 else 0)
            let applicationCount = rowData |> Seq.sumBy (fun r -> if r.Application_flag then 1 else 0)
            
            (gen, count, float utilizationCount / float count * 100.0, float applicationCount / float count * 100.0))
        |> Seq.sortBy (fun (gen, _, _, _) -> 
            match gen with
            | "Молодёж" -> 1
            | _ -> 2)
        |> Seq.toList
    
    let generations = voiceOfferStats |> List.map (fun (gen, _, _, _) -> gen)
    let utilizationRates = voiceOfferStats |> List.map (fun (_, _, util, _) -> util)
    let applicationRates = voiceOfferStats |> List.map (fun (_, _, _, app) -> app)
    
    [
        Chart.Column(utilizationRates, generations, Name = "Утилизация после озвученного оффера (%)")
        Chart.Column(applicationRates, generations, Name = "Заявки после озвученного оффера (%)")
    ]
    |> Chart.combine
    |> Chart.withTitle "Эффективность озвученных офферов по поколениям"
    |> Chart.withXAxisStyle "Поколение клиента"
    |> Chart.withYAxisStyle "Процент"

// 5. Анализ корреляции между временем убеждения и успехом
let persuasionTimeCorrelationChart =
    let dataByGeneration =
        dataWithClientGeneration
        |> List.groupBy fst
        |> List.sortBy (fun (gen, _) -> 
            match gen with
            | "Молодёж" -> 1
            | _ -> 2)
    
    let charts =
        dataByGeneration
        |> List.map (fun (gen, rows) ->
            let rowData = rows |> List.map snd
            let successData =
                rowData
                |> List.map (fun r -> 
                    float r.Offer_time_sec / 60.0, if r.Utilization_flag then 1.0 else 0.0)
            
            let x = successData |> List.map fst
            let y = successData |> List.map snd
            
            Chart.Scatter(
                X = x, Y = y, 
                Mode = StyleParam.Mode.Markers,
                Name = gen,
                Marker = Marker.init(Size = 10, Opacity = 0.5)
            )
        )
    
    charts
    |> Chart.combine
    |> Chart.withTitle "Корреляция времени убеждения и успеха утилизации по поколениям"
    |> Chart.withXAxisStyle "Время убеждения (минуты)"
    |> Chart.withYAxisStyle "Утилизация (0 - нет, 1 - да)"

// Создаем dashboard со всеми графиками
let salesDashboard =
    [
        susceptibilityChart
        persuasionTimeChart
        offerTypeAnalysis
        voiceOfferEffectivenessChart
        persuasionTimeCorrelationChart
    ]
    |> Chart.Grid(3, 2)
    |> Chart.withSize(1600, 1200)

let show _ =
    salesDashboard |> Chart.show