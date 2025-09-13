module DanoHackApi.Example.Models

open Microsoft.ML.Data

[<CLIMutable>]
type TennisMatch = {
    [<LoadColumn(0)>] TourneyId: string
    [<LoadColumn(1)>] TourneyName: string
    [<LoadColumn(2)>] Surface: string
    [<LoadColumn(3)>] DrawSize: single
    [<LoadColumn(4)>] TourneyLevel: string
    [<LoadColumn(5)>] TourneyDate: string
    [<LoadColumn(6)>] MatchNum: single
    [<LoadColumn(7)>] WinnerId: single
    [<LoadColumn(8)>] WinnerSeed: single
    [<LoadColumn(9)>] WinnerEntry: string
    [<LoadColumn(10)>] WinnerName: string
    [<LoadColumn(11)>] WinnerHand: string
    [<LoadColumn(12)>] WinnerHt: single
    [<LoadColumn(13)>] WinnerIoc: string
    [<LoadColumn(14)>] WinnerAge: single
    [<LoadColumn(15)>] LoserId: single
    [<LoadColumn(16)>] LoserSeed: single
    [<LoadColumn(17)>] LoserEntry: string
    [<LoadColumn(18)>] LoserName: string
    [<LoadColumn(19)>] LoserHand: string
    [<LoadColumn(20)>] LoserHt: single
    [<LoadColumn(21)>] LoserIoc: string
    [<LoadColumn(22)>] LoserAge: single
    [<LoadColumn(23)>] Score: string
    [<LoadColumn(24)>] BestOf: single
    [<LoadColumn(25)>] Round: string
    [<LoadColumn(26)>] Minutes: single
    [<LoadColumn(27)>] WAce: single
    [<LoadColumn(28)>] WDf: single
    [<LoadColumn(29)>] WSvpt: single
    [<LoadColumn(30)>] W1stIn: single
    [<LoadColumn(31)>] W1stWon: single
    [<LoadColumn(32)>] W2ndWon: single
    [<LoadColumn(33)>] WSvGms: single
    [<LoadColumn(34)>] WBpSaved: single
    [<LoadColumn(35)>] WBpFaced: single
    [<LoadColumn(36)>] LAce: single
    [<LoadColumn(37)>] LDf: single
    [<LoadColumn(38)>] LSvpt: single
    [<LoadColumn(39)>] L1stIn: single
    [<LoadColumn(40)>] L1stWon: single
    [<LoadColumn(41)>] L2ndWon: single
    [<LoadColumn(42)>] LSvGms: single
    [<LoadColumn(43)>] LBpSaved: single
    [<LoadColumn(44)>] LBpFaced: single
    [<LoadColumn(45)>] WinnerRank: single
    [<LoadColumn(46)>] WinnerRankPoints: single
    [<LoadColumn(47)>] LoserRank: single
    [<LoadColumn(48)>] LoserRankPoints: single
}

[<CLIMutable>]
type EnrichedTennisMatch = {
    OriginalMatch: TennisMatch
    
    RankDifference: single
    AgeDifference: single
    HeightDifference: single
    IsUpset: bool
    WinnerFirstServePercent: single
    LoserFirstServePercent: single
    WinnerServeEffectiveness: single
    LoserServeEffectiveness: single
    WinnerBreakPointConversion: single
    LoserBreakPointConversion: single
    
    ClusterId: uint32
}

[<CLIMutable>]
type ModelInput = {
    [<ColumnName("Features")>]
    Features: single[]
    
    [<ColumnName("Label")>]
    Label: bool
    
    RankDifference: single
    AgeDifference: single
    Surface: string
    TourneyLevel: string
    WinnerFirstServePercent: single
    WinnerServeEffectiveness: single
}

[<CLIMutable>]
type ModelOutput = {
    [<ColumnName("PredictedLabel")>]
    PredictedLabel: bool
    
    [<ColumnName("Probability")>]
    Probability: single
    
    [<ColumnName("Score")>]
    Score: single
}

[<CLIMutable>]
type FeatureImportance = {
    FeatureName: string
    ImportanceScore: single
}

[<CLIMutable>]
type ClusterSummary = {
    ClusterId: int
    Size: int
    AverageRankDifference: single
    AverageAgeDifference: single
    AverageServeEffectiveness: single
    UpsetPercentage: single
}

type PlayerStats = {
    PlayerName: string
    MatchesPlayed: int
    WinPercentage: single
    AverageAcesPerMatch: single
    FirstServePercentage: single
    BreakPointConversion: single
}

type TournamentInsights = {
    TournamentName: string
    AverageMatchDuration: single
    AcesPerMatch: single
    UpsetPercentage: single
    SurfaceStats: Map<string, single>
}

type ApiResponse<'T> = {
    Success: bool
    Data: 'T
    Message: string option
}

type PredictionRequest = {
    PlayerRank: single
    OpponentRank: single
    PlayerAge: single
    OpponentAge: single
    Surface: string
    TournamentLevel: string
}

type PredictionResponse = {
    WinProbability: single
    KeyFactors: FeatureImportance list
}

[<CLIMutable>]
type ClusterResult = {
    PredictedLabel: uint32
    RankDifference: single
    AgeDifference: single
    WinnerServeEffectiveness: single
    WinnerBreakPointConversion: single
}