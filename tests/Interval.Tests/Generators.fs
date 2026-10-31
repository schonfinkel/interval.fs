namespace Interval.Tests

/// Generators built to reach the cases that matter: endpoint collisions, every
/// boundary kind, single points and touching pieces.
module Generators =
    open FsCheck
    open FsCheck.FSharp
    open Interval.Builders
    open Interval.Core

    let private tryBounded (lower, upper) =
        BoundedInterval.TryCreate(lower, upper) |> Result.toOption

    /// A generator defect, not a library failure, so it should stop the run loudly.
    let private bounded endpoints =
        match tryBounded endpoints with
        | Some interval -> interval
        | None -> failwith $"Generator produced invalid endpoints: %A{endpoints}"

    let kind = Gen.elements [ Included; Excluded ]

    /// Even values only: tests probe odd values to observe the space between endpoints.
    let value = Gen.choose (-8, 8) |> Gen.map ((*) 2)

    let private width = Gen.choose (1, 4) |> Gen.map ((*) 2)

    let private pointInterval = value |> Gen.map (fun v -> bounded (inc v, inc v))

    let private properInterval =
        gen {
            let! first = value
            let! extent = width
            let! lowerKind = kind
            let! upperKind = kind

            return
                bounded (
                    { Value = first; Kind = lowerKind },
                    { Value = first + extent
                      Kind = upperKind }
                )
        }

    let boundedInterval = Gen.frequency [ 1, pointInterval; 4, properInterval ]

    /// Proper half-open intervals: the boundary shape composition is sound for.
    let halfOpenInterval =
        gen {
            let! first = value
            let! extent = width
            return bounded (inc first, exc (first + extent))
        }

    let interval =
        gen {
            let! count = Gen.choose (0, 6)
            let! pieces = Gen.listOfLength count boundedInterval
            return Interval.ofSeq pieces
        }

    let relationSet =
        gen {
            let! flags = Gen.listOfLength 13 (Gen.elements [ true; false ])

            return
                RelationSet.toList RelationSet.all
                |> List.zip flags
                |> List.filter fst
                |> List.map snd
                |> RelationSet.ofList
        }

    /// Narrower valid intervals: the lower point, the upper point, then a closed version.
    let shrinkBounded (original: BoundedInterval<int>) =
        [ original.Lower, inc original.Lower.Value
          inc original.Upper.Value, original.Upper
          inc original.Lower.Value, inc original.Upper.Value ]
        |> Seq.choose tryBounded
        |> Seq.filter ((<>) original)

    /// Removes a piece, then narrows a piece. Rebuilding through ofSeq keeps results canonical.
    let shrinkInterval (original: Interval<int>) =
        let pieces = Interval.toList original

        let withoutOne =
            pieces |> Seq.indexed |> Seq.map (fun (i, _) -> List.removeAt i pieces)

        let withOneNarrowed =
            pieces
            |> Seq.indexed
            |> Seq.collect (fun (i, piece) ->
                shrinkBounded piece |> Seq.map (fun smaller -> List.updateAt i smaller pieces))

        Seq.append withoutOne withOneNarrowed |> Seq.map Interval.ofSeq

    /// Registered through FsCheckConfig.arbitrary.
    type Arbitraries =
        static member BoundedInterval() =
            Arb.fromGenShrink (boundedInterval, shrinkBounded)

        static member Interval() =
            Arb.fromGenShrink (interval, shrinkInterval)

        static member RelationSet() = Arb.fromGen relationSet

    /// Every bounded interval on a small grid, for exhaustive small-scope checks.
    let boundedGrid =
        let values = [ 0; 2; 4; 6; 8 ]
        let kinds = [ Included; Excluded ]

        [ for first in values do
              for last in values do
                  for lowerKind in kinds do
                      for upperKind in kinds -> { Value = first; Kind = lowerKind }, { Value = last; Kind = upperKind } ]
        |> List.choose tryBounded

    /// Every proper closed interval over six points: enough to order three intervals every way.
    let closedTriples =
        let intervals =
            [ for first in 0..5 do
                  for last in first + 1 .. 5 -> bounded (inc first, inc last) ]

        [ for a in intervals do
              for b in intervals do
                  for c in intervals -> a, b, c ]
