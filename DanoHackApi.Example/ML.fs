module DanoHackApi.Example.ML

open System
open System.Diagnostics
open System.Text
open System.Threading.Tasks
open DanoHackApi.Example.DatasetProvider
open DanoHackApi.Example.Models
open DanoHackApi.Example.ModelsUtils
open Microsoft.Extensions.Logging
open Microsoft.ML
open Microsoft.ML.Data
open DanoHackApi.Example.Utils

type MachineLearningService(
    dataset: DatasetProviderService,
    logger: ILogger<MachineLearningService>
) =

    let mlContext = MLContext(seed = 42)
    let mutable trainedModel: ITransformer option = None

    let metricsToString (metrics: CalibratedBinaryClassificationMetrics) (elapsed: TimeSpan) =
        let sb = StringBuilder()
        %sb.AppendLine($"Training finished in {elapsed.TotalSeconds:F1} sec")
        %sb.AppendLine($"Accuracy: {metrics.Accuracy:P2}")
        %sb.AppendLine($"F1Score: {metrics.F1Score:P2}")
        %sb.AppendLine($"AUC: {metrics.AreaUnderRocCurve:P2}")
        %sb.AppendLine($"LogLoss: {metrics.LogLoss:F4}")
        sb.ToString()

    let buildPipeline() =
        mlContext.Transforms.Categorical.OneHotEncoding("SurfaceEncoded", "Surface")
            .Append(mlContext.Transforms.Categorical.OneHotEncoding("RoundEncoded", "Round"))
            .Append(
                mlContext.Transforms.Concatenate(
                    "Features",
                    [| "RankDifference"
                       "AgeDifference"
                       "WinnerFirstServePercent"
                       "WinnerServeEffectiveness"
                       "WinnerBreakPointConversion"
                       "SurfaceEncoded"
                       "TourneyLevelEncoded"
                       "RoundEncoded" |]
                )
            )
            .Append(
                mlContext.BinaryClassification.Trainers.LightGbm(
                    labelColumnName = "Label",
                    featureColumnName = "Features"
                )
            )
            

    member this.TrainModel() = task {
        try
            logger.LogInformation("Loading dataset...")
            let! dataView = dataset.createFromFile(fun path -> task {
                return mlContext.Data.LoadFromTextFile<TennisMatch>(
                    path, hasHeader = true, separatorChar = ',')
            })

            logger.LogInformation("Preparing training data...")
            let enriched =
                mlContext.Data.CreateEnumerable<TennisMatch>(dataView, reuseRowObject = false)
                |> Seq.map (fun m -> toModelInput (toEnrichedMatch m) true)
                |> mlContext.Data.LoadFromEnumerable

            let split = mlContext.Data.TrainTestSplit(enriched, testFraction = 0.2)

            let pipeline = buildPipeline()
            let sw = Stopwatch.StartNew()

            let model = pipeline.Fit(split.TrainSet)
            trainedModel <- Some model

            let predictions = model.Transform(split.TestSet)
            let metrics = mlContext.BinaryClassification.Evaluate(predictions, "Label")

            sw.Stop()
            logger.LogInformation(metricsToString metrics sw.Elapsed)

            return true
        with ex ->
            logger.LogError(ex, "Training failed")
            return false
    }

    member this.Predict(req: PredictionRequest) =
        match trainedModel with
        | None ->
            logger.LogError("Model not trained")
            None
        | Some model ->
            try
                let engine = mlContext.Model.CreatePredictionEngine<ModelInput, ModelOutput>(model)

                let fakeMatch = {
                    // создаём "фиктивный" матч на основе запроса
                    OriginalMatch = Unchecked.defaultof<TennisMatch>
                    RankDifference = req.PlayerRank - req.OpponentRank
                    AgeDifference = req.PlayerAge - req.OpponentAge
                    HeightDifference = 0.0f
                    IsUpset = false
                    WinnerFirstServePercent = 60.0f
                    LoserFirstServePercent = 60.0f
                    WinnerServeEffectiveness = 0.6f
                    LoserServeEffectiveness = 0.6f
                    WinnerBreakPointConversion = 30.0f
                    LoserBreakPointConversion = 30.0f
                    ClusterId = 0u
                }

                let input = toModelInput fakeMatch true
                let pred = engine.Predict(input)

                Some {
                    WinProbability = pred.Probability
                    KeyFactors =
                        [ { FeatureName = "RankDifference"; ImportanceScore = input.RankDifference }
                          { FeatureName = "AgeDifference"; ImportanceScore = input.AgeDifference }
                          { FeatureName = "FirstServe"; ImportanceScore = input.WinnerFirstServePercent }
                          { FeatureName = "ServeEffectiveness"; ImportanceScore = input.WinnerServeEffectiveness } ]
                }
            with ex ->
                logger.LogError(ex, "Prediction failed")
                None

    /// Анализ важности признаков через кросс-валидацию
    member this.AnalyzeFeatures() = task {
        match trainedModel with
        | None ->
            logger.LogError("Model not trained")
            return None
        | Some _ ->
            try
                let! dataView = dataset.createFromFile(fun path -> task {
                    return mlContext.Data.LoadFromTextFile<TennisMatch>(
                        path, hasHeader = true, separatorChar = ',')
                })

                let enriched =
                    mlContext.Data.CreateEnumerable<TennisMatch>(dataView, reuseRowObject = false)
                    |> Seq.map (fun m -> toModelInput (toEnrichedMatch m) true)
                    |> mlContext.Data.LoadFromEnumerable

                let pipeline = buildPipeline()
                let cv = mlContext.BinaryClassification.CrossValidate(enriched, pipeline, numberOfFolds = 5)

                let avgAuc = cv |> Seq.averageBy (fun r -> r.Metrics.AreaUnderRocCurve)
                logger.LogInformation($"Avg AUC (CV): {avgAuc:P2}")
                return Some avgAuc
            with ex ->
                logger.LogError(ex, "Feature importance failed")
                return None
    }
    
    member this.ClusterMatches() = task {
        try
            let! rawDataView =
                dataset.createFromFile(fun path -> task {
                    return mlContext.Data.LoadFromTextFile<TennisMatch>(path, hasHeader = true, separatorChar = ',')
                })

            // Создадим набор для кластеризации (используем заранее рассчитанные признаки)
            let clusterInputs =
                mlContext.Data.CreateEnumerable<TennisMatch>(rawDataView, reuseRowObject = false)
                |> Seq.map toEnrichedMatch
                |> Seq.map (fun e ->
                    // используем те же поля, что и выше — помещаем в ModelInput.Features
                    { Features = [| e.RankDifference; e.AgeDifference; e.WinnerServeEffectiveness; e.WinnerBreakPointConversion |]
                      Label = false
                      RankDifference = e.RankDifference
                      AgeDifference = e.AgeDifference
                      Surface = e.OriginalMatch.Surface
                      TourneyLevel = e.OriginalMatch.TourneyLevel
                      WinnerFirstServePercent = e.WinnerFirstServePercent
                      WinnerServeEffectiveness = e.WinnerServeEffectiveness } )
                |> Seq.toList

            let data = mlContext.Data.LoadFromEnumerable(clusterInputs)

            // KMeans trainer (упростим — число кластеров 3)
            let pipeline = (mlContext.Clustering.Trainers.KMeans(numberOfClusters = 3, featureColumnName = "Features") :> IEstimator<ITransformer>)
            let model = pipeline.Fit(data)
            let preds = model.Transform(data)
            

            let results =
                mlContext.Data.CreateEnumerable<ClusterResult>(preds, reuseRowObject = false)
                |> Seq.toList

            // Сводка по кластерам
            let summaries =
                results
                |> Seq.groupBy (fun r -> int r.PredictedLabel)
                |> Seq.map (fun (cid, items) ->
                    let arr = items |> Seq.toArray
                    let size = arr.Length
                    let avgRankDiff = if size = 0 then 0.0f else (arr |> Seq.averageBy (fun x -> float32 x.RankDifference))
                    let avgAgeDiff = if size = 0 then 0.0f else (arr |> Seq.averageBy (fun x -> float32 x.AgeDifference))
                    let avgServeEff = if size = 0 then 0.0f else (arr |> Seq.averageBy (fun x -> float32 x.WinnerServeEffectiveness))
                    { ClusterId = cid
                      Size = size
                      AverageRankDifference = avgRankDiff
                      AverageAgeDifference = avgAgeDiff
                      AverageServeEffectiveness = avgServeEff
                      UpsetPercentage = 0.0f } // можно посчитать, если пометим IsUpset в исходных данных
                )
                |> Seq.toArray

            logger.LogInformation($"Clustering finished. Found {summaries.Length} clusters.")
            return Some summaries
        with ex ->
            logger.LogError(ex, "Error during clustering")
            return None
    }

    member this.GetPlayerStats(playerName: string) = task {
        try
            let! rawDataView =
                dataset.createFromFile(fun path -> task {
                    return mlContext.Data.LoadFromTextFile<TennisMatch>(path, hasHeader = true, separatorChar = ',')
                })

            let matches = mlContext.Data.CreateEnumerable<TennisMatch>(rawDataView, reuseRowObject = false) |> Seq.toArray

            let playerMatches = matches |> Array.filter (fun m -> m.WinnerName = playerName || m.LoserName = playerName)

            if playerMatches.Length = 0 then
                logger.LogWarning($"Player '{playerName}' not found in dataset")
                return None
            else
                let wins = playerMatches |> Array.filter (fun m -> m.WinnerName = playerName) |> Array.length
                let total = playerMatches.Length
                let winPct = if total = 0 then 0.0f else float32 wins / float32 total * 100.0f

                let totalAces =
                    playerMatches
                    |> Array.sumBy (fun m -> if m.WinnerName = playerName then m.WAce else m.LAce)

                let avgAces = if total = 0 then 0.0f else float32 totalAces / float32 total

                let firstServePercents =
                    playerMatches
                    |> Array.choose (fun m ->
                        if m.WinnerName = playerName then
                            if m.WSvpt > 0.0f then Some (m.W1stIn / m.WSvpt * 100.0f) else None
                        elif m.LoserName = playerName then
                            if m.LSvpt > 0.0f then Some (m.L1stIn / m.LSvpt * 100.0f) else None
                        else None)

                let avgFirstServe = if firstServePercents.Length = 0 then 0.0f else firstServePercents |> Array.average

                let bpConversions =
                    playerMatches
                    |> Array.choose (fun m ->
                        if m.WinnerName = playerName then
                            if m.WBpFaced > 0.0f then Some (m.WBpSaved / m.WBpFaced * 100.0f) else None
                        elif m.LoserName = playerName then
                            if m.LBpFaced > 0.0f then Some (m.LBpSaved / m.LBpFaced * 100.0f) else None
                        else None)

                let avgBpConv = if bpConversions.Length = 0 then 0.0f else bpConversions |> Array.average

                let stats = {
                    PlayerName = playerName
                    MatchesPlayed = total
                    WinPercentage = winPct
                    AverageAcesPerMatch = avgAces
                    FirstServePercentage = float32 avgFirstServe
                    BreakPointConversion = float32 avgBpConv
                }

                return Some stats
        with ex ->
            logger.LogError(ex, "Error getting player stats")
            return None
    }
