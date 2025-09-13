module DanoHackApi.Example.ModelsUtils

open DanoHackApi.Example.Models

let toEnrichedMatch (matchData: TennisMatch) =
    {
        OriginalMatch = matchData
        RankDifference = matchData.WinnerRank - matchData.LoserRank
        AgeDifference = matchData.WinnerAge - matchData.LoserAge
        HeightDifference = matchData.WinnerHt - matchData.LoserHt
        IsUpset = matchData.WinnerRank > matchData.LoserRank // Если победитель имеет более низкий рейтинг
        WinnerFirstServePercent = matchData.W1stIn / matchData.WSvpt * 100.0f
        LoserFirstServePercent = matchData.L1stIn / matchData.LSvpt * 100.0f
        WinnerServeEffectiveness = (matchData.W1stWon + matchData.W2ndWon) / matchData.WSvpt
        LoserServeEffectiveness = (matchData.L1stWon + matchData.L2ndWon) / matchData.LSvpt
        WinnerBreakPointConversion = matchData.WBpSaved / matchData.WBpFaced * 100.0f
        LoserBreakPointConversion = matchData.LBpSaved / matchData.LBpFaced * 100.0f
        ClusterId = 0u
    }

let toModelInput (enrichedMatch: EnrichedTennisMatch) (label: bool) =
    {
        Features = [|
            enrichedMatch.RankDifference
            enrichedMatch.AgeDifference
            enrichedMatch.WinnerFirstServePercent
            enrichedMatch.WinnerServeEffectiveness
            enrichedMatch.WinnerBreakPointConversion
        |]
        Label = label
        RankDifference = enrichedMatch.RankDifference
        AgeDifference = enrichedMatch.AgeDifference
        Surface = enrichedMatch.OriginalMatch.Surface
        TourneyLevel = enrichedMatch.OriginalMatch.TourneyLevel
        WinnerFirstServePercent = enrichedMatch.WinnerFirstServePercent
        WinnerServeEffectiveness = enrichedMatch.WinnerServeEffectiveness
    }