(**
---
title: Temperature in Germany
category: Datasets
categoryindex: 1
index: 6
---

[![Binder]({{root}}img/badge-binder.svg)](https://mybinder.org/v2/gh/plotly/Plotly.NET/gh-pages?filepath={{fsdocs-source-basename}}.ipynb)&emsp;
[![Script]({{root}}img/badge-script.svg)]({{root}}{{fsdocs-source-basename}}.fsx)&emsp;
[![Notebook]({{root}}img/badge-notebook.svg)]({{root}}{{fsdocs-source-basename}}.ipynb)

# The _Deutsche Wetterdienst_ datasets

**Table of contents**

- [Description]()
- [How to use]()
- [Examples]()

## Description

The official website from the ["Deutsche Wetterdienst"](https://www.dwd.de/DE/leistungen/klimadatendeutschland/klimadatendeutschland.html) offers regional climate data with daily or monthly measurements.


## How to use


*)

#r "nuget: Fsharp.Data"
#r "nuget: FSharp.Stats"
#r "nuget: Plotly.NET, 2.0.0-preview.6"
#r "nuget: Deedle"

// Get data from Deutscher Wetterdienst
// Explanation for Abbreviations: https://www.dwd.de/DE/leistungen/klimadatendeutschland/beschreibung_tagesmonatswerte.html
let rawData = FSharp.Data.Http.RequestString @"https://www.dwd.de/DE/leistungen/_config/leistungsteckbriefPublication.txt?view=nasPublication&nn=16102&imageFilePath=8524108716561010473591414984380285860348440627905136707616031491601294722759953476578874542442119146105183517945099480938740343906988335742522410754862714750285564712567437754031667430876229001517155807795323254373913159234471816274144126039683579197&download=true"

open System
open System.Text.RegularExpressions

open Deedle

//The data is formatted in a char column-wise structure, to transform it into a seperator-delimited format, we have to do prior formatting:

let processedData = 
    // First separate the huge string in lines
    rawData.Split([|'\n'|], StringSplitOptions.RemoveEmptyEntries)
    // Skip the first 5 rows until the real data starts, also skip the last row (length-2) to remove a "</pre>" at the end
    |> fun arr -> arr.[5..arr.Length-2]
    |> Array.map (fun data -> 
        // Regex pattern that will match groups of whitespace
        let whitespacePattern = @"\s+"
        // This is needed to tell regex to replace hits with a tabulator
        let matchEval = MatchEvaluator(fun _ -> "\t" )
        // The original data columns are separated by different amounts of whitespace.
        // Therefore, we need a flexible string parsing option to replace any amount of whitespace with a single tabulator.
        // This is done with the regex pattern above and the fsharp core library "System.Text.RegularExpressions" 
        let tabseparated = Regex.Replace(data, whitespacePattern, matchEval)
        tabseparated
        // Split each row by tabulator will return rows with an equal amount of values, which we can access.
        |> fun dataStr -> dataStr.Split([|"\t"|], StringSplitOptions.RemoveEmptyEntries)
        |> fun dataArr -> 
            // Second value is the date of measurement, which we will parse to the DateTime type
            DateTime.ParseExact(dataArr.[1], "yyyyMMdd", Globalization.CultureInfo.InvariantCulture),
            // 5th value is minimal temperature at that date.
            float dataArr.[4],
            // 6th value is average temperature over 24 timepoints at that date.
            float dataArr.[5],
            // 7th value is maximal temperature at that date.
            float dataArr.[6]
    )
    // Sort by date
    |> Array.sortBy (fun (day,tn,tm,tx) -> day)
    // Unzip the array of value tuples, to make the different values easier accessible
    |> fun arr -> 
        arr |> Array.map (fun (day,tn,tm,tx) -> day.ToShortDateString()),
        arr |> Array.map (fun (day,tn,tm,tx) -> tm),
        arr |> Array.map (fun (day,tn,tm,tx) -> tx),
        arr |> Array.map (fun (day,tn,tm,tx) -> tn)

(*** include-value: processedData***)

(**
## Examples
*)


open Plotly.NET

// Because our data set is already rather wide we want to move the legend from the right side of the plot
// to the right center. As this function is not defined for fsharp we will use the underlying js bindings (https://plotly.com/javascript/legend/#positioning-the-legend-inside-the-plot).
// Declarative style in F# using underlying DynamicObj
// https://plotly.net/#Declarative-style-in-F-using-the-underlying
let legend = 
    let tmp = Legend()
    tmp?yanchor <- "top"
    tmp?y <- 0.99
    tmp?xanchor <- "left"
    tmp?x <- 0.5
    tmp

/// This function will take 'processedData' as input and return a range chart with a line for the average temperature
/// and a different colored area for the range between minimal and maximal temperature at that date.
let createTempChart (days,tm,tmUpper,tmLower) =
    Chart.Range(
        // data arrays
        days, tm, tmUpper, tmLower,
        StyleParam.Mode.Lines_Markers,
        Color="#3D1244",
        RangeColor="#F99BDE",
        // Name for line in legend
        Name="Average temperature over 24 timepoints each day",
        // Name for lower point when hovering over chart
        LowerName="Min temp",
        // Name for upper point when hovering over chart
        UpperName="Max temp"
    )
    // Configure the chart with the legend from above
    |> Chart.withLegend legend
    // Add name to y axis
    |> Chart.withY_AxisStyle("daily temperature [°C]")
    |> Chart.withSize (1000.,600.)

/// Chart for original data set 
let rawChart =
    processedData 
    |> createTempChart

rawChart |> GenericChart.toChartHTML
(***include-it-raw***)

open FSharp.Stats

let smootheTemp ws order (days,tm,tmUpper,tmLower) =
    let tm' = Signal.Filtering.savitzkyGolay ws order 0 1 tm
    let tmUpper' = Signal.Filtering.savitzkyGolay ws order 0 1 tmUpper
    let tmLower' = Signal.Filtering.savitzkyGolay ws order 0 1 tmLower
    days,tm',tmUpper',tmLower'

let smoothedChart =
    processedData
    |> smootheTemp 31 4
    |> createTempChart 

smoothedChart |> GenericChart.toChartHTML
(***include-it-raw***)   
