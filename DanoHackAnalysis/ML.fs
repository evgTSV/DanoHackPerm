module MLAnalysis

open System
open Microsoft.ML
open Microsoft.ML.Data
open Data

[<CLIMutable>]
type InputData = {
    client_age: float32
    client_gender: string
    foreign_flg: Single
    suppress_call_flg: Single
    suppress_email_flg: Single
    suppress_limit_flg: Single
    suppress_sms_flg: Single
    wait_time_sec: float32
    appeal_time_sec: float32
    employee_dialog_time_sec: float32
    repeated_appeal_flg: Single
    negative_flg: Single
    csat_score: float32
    transfer_flg: Single
    task_flg: Single
    work_experience_day_cnt: float32
    employee_type: string
    employee_gender: string
    business_line: string
    Utilization_flag: Single
    Client_agree_flg: Single
}

let inputData =
    data.Rows |> Seq.map (fun row -> {
      client_age = float32 row.Client_age
      client_gender = row.Client_gender
      foreign_flg = row.Foreign_flg |> Convert.ToSingle
      suppress_call_flg = row.Suppress_call_flg |> Convert.ToSingle
      suppress_email_flg = row.Suppress_email_flg |> Convert.ToSingle
      suppress_limit_flg = row.Suppress_limit_flg |> Convert.ToSingle
      suppress_sms_flg = row.Suppress_sms_flg |> Convert.ToSingle
      wait_time_sec = float32 row.Wait_time_sec
      appeal_time_sec = float32 row.Appeal_time_sec
      employee_dialog_time_sec = float32 row.Employee_dialog_time_sec
      repeated_appeal_flg = row.Repeated_appeal_flg |> Convert.ToSingle
      negative_flg = row.Negative_flg |> Convert.ToSingle
      csat_score = float32 row.Csat_score
      transfer_flg = row.Transfer_flg |> Convert.ToSingle
      task_flg = row.Task_flg |> Convert.ToSingle
      work_experience_day_cnt = float32 row.Work_experience_day_cnt
      employee_type = row.Employee_type
      employee_gender = row.Employee_gender
      business_line = row.Business_line
      Utilization_flag = row.Utilization_flag |> Convert.ToSingle
      Client_agree_flg = row.Client_agree_flg |> Convert.ToSingle
    })

[<CLIMutable>]
type ClusterPrediction = {
    [<ColumnName("PredictedLabel")>]
    ClusterId: uint32
    [<ColumnName("Score")>]
    Distances: float32[]
}

let append (estimator : IEstimator<'a>) (pipeline : IEstimator<'b>)  =
      match pipeline with
      | :? IEstimator<ITransformer> as p ->
          p.Append estimator
      | _ -> failwith "The pipeline has to be an instance of IEstimator<ITransformer>."

let runClustering() =
    let mlContext = MLContext(seed = Nullable 42)

    let trainData = mlContext.Data.LoadFromEnumerable(inputData)

    // Обработка категориальных
    let pipeline =
        mlContext.Transforms.Categorical.OneHotEncoding("client_gender")
            |> append(mlContext.Transforms.Categorical.OneHotEncoding("employee_type"))
            |> append(mlContext.Transforms.Categorical.OneHotEncoding("employee_gender"))
            |> append(mlContext.Transforms.Categorical.OneHotEncoding("business_line"))
            |> append(mlContext.Transforms.Concatenate("Features",
                "client_age", "client_gender", "foreign_flg",
                "suppress_call_flg", "suppress_email_flg", "suppress_limit_flg", "suppress_sms_flg",
                "wait_time_sec", "appeal_time_sec", "employee_dialog_time_sec",
                "repeated_appeal_flg", "negative_flg", "csat_score",
                "transfer_flg", "task_flg", "work_experience_day_cnt",
                "employee_type", "employee_gender", "business_line",
                "Utilization_flag", "Client_agree_flg"))
            |> append(mlContext.Transforms.NormalizeMinMax("Features"))
            |> append(mlContext.Clustering.Trainers.KMeans("Features", numberOfClusters = 3))

    let model = pipeline.Fit(trainData)

    let predictions = model.Transform(trainData)

    // let metrics = mlContext.Clustering.Evaluate(predictions, "Features", "Score")
    //
    // printfn "=== Кластеризация клиентов ==="
    // printfn "Среднее расстояние: %.3f" metrics.AverageDistance
    // printfn "Davies-Bouldin Index: %.3f" metrics.DaviesBouldinIndex

    let preview = predictions.Preview(10)
    for row in preview.RowView do
        row.Values
        |> Seq.iter (fun kv -> printf "%s=%O; " kv.Key kv.Value)
        printfn ""

    model