open System
open System.IO

let reduce() = 
    use reader = File.OpenText "../DanoHackAnalysis/data.csv"

    use writer = File.CreateText("reduced.csv")
    use writer2 = File.CreateText("reduced_2.csv")

    for i in 1 .. 60000 do
        
        reader.ReadLine()
        |> writer.WriteLine

    reader.ReadToEnd()
    |> _.Split("\n")
    |> Array.iter writer2.WriteLine

reduce()
printfn "Finished"