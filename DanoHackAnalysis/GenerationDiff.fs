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
    | age when age <= 35M -> "Молодёжь"
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
    ZeroAgreementRate: float
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
        
    let zeroAgreementCount = 
        match agreementStats.TryFind 0 with
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
        |> List.choose (fun r -> if r.Offer_time_sec > 0M then Some r.Offer_time_sec else None)
        |> fun l -> if l.Length > 0 then List.average l else 0M
    
    {
        Generation = generation
        Count = count
        UtilizationRate = float utilizationCount / float count * 3000.0
        ApplicationRate = float applicationCount / float count * 2000.0
        VoiceOfferRate = float voiceOfferCount / float count * 100.0
        AvgOfferTime = avgOfferTime
        AgreementRate = 
            if totalAgreementResponses > 0 then 
                float positiveAgreementCount / float totalAgreementResponses * 100.0 
            else 0.0
        PositiveAgreementRate = float positiveAgreementCount / float count * 500.0
        ZeroAgreementRate = float zeroAgreementCount / float count * 500.0
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
        | "Молодёжь" -> 1
        | _ -> 2)
    |> Seq.toList

// 1. График восприимчивости к предложениям по поколениям
let susceptibilityChart =
    let generations = generationStats |> List.map (fun x -> x.Generation)
    
    let utilizationRates = generationStats |> List.map (fun x -> x.UtilizationRate)
    let applicationRates = generationStats |> List.map (fun x -> x.ApplicationRate)
    let agreementRates = generationStats |> List.map (fun x -> x.AgreementRate)
    let positiveAgreementRates = generationStats |> List.map (fun x -> x.PositiveAgreementRate)
    let zeroAgreementRates = generationStats |> List.map (fun x -> x.ZeroAgreementRate)
    
    [
        Chart.Column(utilizationRates, generations, Name = "Утилизация (%)")
        Chart.Column(applicationRates, generations, Name = "Заявки (%)")
        Chart.Column(agreementRates, generations, Name = "Согласие (%)")
        Chart.Column(positiveAgreementRates, generations, Name = "Положительное согласие (%)")
        Chart.Column(zeroAgreementRates, generations, Name = "Согласие не указано (%)")
    ]
    |> Chart.combine
    |> Chart.withTitle "Восприимчивость к предложениям по поколениям клиентов"
    |> Chart.withXAxisStyle "Поколение клиента"
    |> Chart.withYAxisStyle "Процент"

// 2. График времени на убеждение по поколениям
let persuasionTimeChart =
    let generations = generationStats |> List.map (fun x -> x.Generation)
    let avgOfferTimes = generationStats |> List.map (fun x -> x.AvgOfferTime)
    
    Chart.Column(
        avgOfferTimes,
        generations,
        TextPosition = StyleParam.TextPosition.Outside
    )
    |> Chart.withTitle "Среднее время убеждения по поколениям"
    |> Chart.withXAxisStyle "Поколение клиента"
    |> Chart.withYAxisStyle "Время (секунды)"

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
            | "Молодёжь" -> 1
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
    dataWithClientGeneration
    |> List.groupBy fst
    |> List.map (fun (gen, rows) ->
        let success = 
            rows 
            |> List.map snd
            |> List.choose (fun r -> 
                if r.Utilization_flag then Some (float r.Offer_time_sec) else None)
        let fail = 
            rows 
            |> List.map snd
            |> List.choose (fun r -> 
                if not r.Utilization_flag then Some (float r.Offer_time_sec) else None)
        [
            Chart.BoxPlot(success, Name = $"{gen} — Успех")
            Chart.BoxPlot(fail, Name = $"{gen} — Нет")
        ]
        |> Chart.combine
    )
    |> Chart.combine
    |> Chart.withTitle "Время убеждения: успешная vs. неуспешная утилизация"
    |> Chart.withYAxisStyle "Время (секунды)"
    |> Chart.withLegendStyle (
        Orientation = StyleParam.Orientation.Horizontal,
        X = 0.5,
        XAnchor = StyleParam.XAnchorPosition.Center    
    )

let salesDashboard =
    [
        susceptibilityChart
        persuasionTimeChart
        offerTypeAnalysis
        voiceOfferEffectivenessChart
        persuasionTimeCorrelationChart
    ]

let show _ =
    salesDashboard
    |> List.iter Chart.show