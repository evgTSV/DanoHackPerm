module Data

open FSharp.Data

[<Literal>]
let FilePath = "data.csv"

type Provider = CsvProvider<FilePath>
let data = Provider.GetSample()