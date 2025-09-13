module GenderDiff

open System
open FSharp.Data
open Plotly.NET
open Plotly.NET.LayoutObjects
open Plotly.NET.TraceObjects
open System.Collections.Generic
open Data

// Создаем тип для хранения данных о гендерных комбинациях
type GenderCombinationStats = {
    Combination: string
    Count: int
    CsatScores: decimal list
    UtilizationRate: float
    NegativeRate: float
    TransferRate: float
    TaskRate: float
    AvgWaitTime: float
    AvgAppealTime: float
    AvgEmployeeDialogTime: float
}

let calculateGroupStats (combination: string) (rows: seq<Provider.Row>) =
    let rowsList = rows |> Seq.toList
    let count = rowsList.Length
    
    let csatScores = 
        rowsList 
        |> List.map (_.Csat_score)
    
    let utilizationCount = rowsList |> List.sumBy (fun r -> if r.Utilization_flag then 1 else 0)
    let negativeCount = rowsList |> List.sumBy (fun r -> if r.Negative_flg then 1 else 0)
    let transferCount = rowsList |> List.sumBy (fun r -> if r.Transfer_flg then 1 else 0)
    let taskCount = rowsList |> List.sumBy (fun r -> if r.Task_flg then 1 else 0)
    
    let avgWaitTime = rowsList |> List.averageBy (fun r -> float r.Wait_time_sec)
    let avgAppealTime = rowsList |> List.averageBy (fun r -> float r.Appeal_time_sec)
    let avgEmployeeDialogTime = rowsList |> List.averageBy (fun r -> float r.Employee_dialog_time_sec)
    
    {
        Combination = combination
        Count = count
        CsatScores = csatScores
        UtilizationRate = float utilizationCount / float count * 50.
        NegativeRate = float negativeCount / float count
        TransferRate = float transferCount / float count
        TaskRate = float taskCount / float count
        AvgWaitTime = avgWaitTime * 100.
        AvgAppealTime = avgAppealTime
        AvgEmployeeDialogTime = avgEmployeeDialogTime
    }

let genderCombinations = 
    data.Rows
    |> Seq.groupBy (fun r -> (r.Client_gender, r.Employee_gender))
    |> Seq.map (fun ((cg, eg), rows) -> 
        let combination = sprintf "%s-%s" cg eg
        calculateGroupStats combination rows)
    |> Seq.sortBy (fun x -> x.Combination)
    |> Seq.toList


// 1. График распределения количества обращений по комбинациям
let combinationCountChart =
    let combinations = genderCombinations |> List.map (fun x -> x.Combination)
    let counts = genderCombinations |> List.map (fun x -> x.Count)
    
    Chart.Column(
        values = counts,
        Keys = combinations,
        TextPosition = StyleParam.TextPosition.Outside
    )
    |> Chart.withTitle "Количество обращений по гендерным комбинациям"
    |> Chart.withXAxisStyle "Гендерная комбинация (Клиент-Оператор)"
    |> Chart.withYAxisStyle "Количество обращений"

// 2. График CSAT scores с violin plot
let csatViolinChart =
    let data = 
        genderCombinations
        |> List.collect (fun stats -> 
            stats.CsatScores 
            |> List.map (fun score -> (stats.Combination, float score)))
    
    let combinations = data |> List.map fst |> List.distinct
    
    combinations
    |> List.map (fun comb ->
        let scores = data |> List.filter (fun (c, _) -> c = comb) |> List.map snd
        Chart.Violin(scores, Name = comb, Points = StyleParam.JitterPoints.All)
    )
    |> Chart.combine
    |> Chart.withTitle "Распределение CSAT оценок по гендерным комбинациям"
    |> Chart.withXAxisStyle "Гендерная комбинация"

// 3. Сравнение метрик эффективности (нормализованных)
let normalizedMetricsChart =
    let combinations = genderCombinations |> List.map (fun x -> x.Combination)
    
    let utilizationRates = genderCombinations |> List.map (fun x -> x.UtilizationRate * 100.0)
    let negativeRates = genderCombinations |> List.map (fun x -> x.NegativeRate * 100.0)
    let transferRates = genderCombinations |> List.map (fun x -> x.TransferRate * 100.0)
    let taskRates = genderCombinations |> List.map (fun x -> x.TaskRate * 100.0)
    
    [
        Chart.Column(utilizationRates, combinations, Name = "Утилизация (%)")
        Chart.Column(negativeRates, combinations, Name = "Негативные обращения (%)")
        Chart.Column(transferRates, combinations, Name = "Трансферы (%)")
        Chart.Column(taskRates, combinations, Name = "Таски (%)")
    ]
    |> Chart.combine
    |> Chart.withTitle "Метрики эффективности по гендерным комбинациям (нормализованные)"
    |> Chart.withXAxisStyle "Гендерная комбинация"
    |> Chart.withYAxisStyle "Процент"

// 4. Сравнение временных показателей
let timeMetricsChart =
    let combinations = genderCombinations |> List.map (fun x -> x.Combination)
    
    let waitTimes = genderCombinations |> List.map (fun x -> x.AvgWaitTime / 60.0) // в минутах
    let appealTimes = genderCombinations |> List.map (fun x -> x.AvgAppealTime / 60.0) // в минутах
    let dialogTimes = genderCombinations |> List.map (fun x -> x.AvgEmployeeDialogTime / 60.0) // в минутах
    
    [
        Chart.Column(waitTimes, combinations, Name = "Среднее время ожидания (мин)")
        Chart.Column(appealTimes, combinations, Name = "Среднее время обращения (мин)")
        Chart.Column(dialogTimes, combinations, Name = "Среднее время диалога (мин)")
    ]
    |> Chart.combine
    |> Chart.withTitle "Временные показатели по гендерным комбинациям"
    |> Chart.withXAxisStyle "Гендерная комбинация"
    |> Chart.withYAxisStyle "Время (минуты)"

// Создаем dashboard со всеми графиками
let dashboard =
    [
        combinationCountChart
        csatViolinChart
        normalizedMetricsChart
        timeMetricsChart
    ]
    |> Chart.Grid(3, 2)
    |> Chart.withSize(1400, 1000)

// Показываем dashboard
let show _ =
    dashboard |> Chart.show

    printfn "Статистика по гендерным комбинациям:"
    printfn "Комбинация\tОбращений\tУтилизация\tНегатив\tТрансферы\tТаски\tРешение проблем"
    genderCombinations
    |> List.iter (fun stats ->
        printfn $"%s{stats.Combination}\t%d{stats.Count}\t%.2f{stats.UtilizationRate * 100.0}%%\t%.2f{stats.NegativeRate * 100.0}%%\t%.2f{stats.TransferRate * 100.0}%%\t%.2f{stats.TaskRate * 100.0}%%"
    )