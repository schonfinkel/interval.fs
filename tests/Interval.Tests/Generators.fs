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

    /// Strictly narrower valid intervals: either end as a point, or either half around
    /// an even midpoint strictly inside.
    let shrinkBounded (original: BoundedInterval<int>) =
        let first, last = original.Lower.Value, original.Upper.Value

        let halves =
            if last - first >= 4 then
                let middle = first + 2 * ((last - first) / 4)
                [ original.Lower, inc middle; inc middle, original.Upper ]
            else
                []

        [ original.Lower, inc first; inc last, original.Upper ] @ halves
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

    let private fragmentsOf (piece: BoundedInterval<int>) =
        let first, last = piece.Lower.Value, piece.Upper.Value

        let split =
            gen {
                let! middle = Gen.choose (first + 1, last - 1)
                return [ bounded (piece.Lower, inc middle); bounded (inc middle, piece.Upper) ]
            }

        if first = last then
            Gen.constant [ piece; piece ]
        else
            Gen.oneof [ Gen.constant [ piece ]; Gen.constant [ piece; piece ]; split ]

    /// An interval and a shuffled, redundant set of fragments that cover exactly its values.
    let intervalWithFragments =
        gen {
            let! whole = interval

            let! fragments =
                Interval.toList whole
                |> List.map fragmentsOf
                |> Gen.sequenceToList
                |> Gen.map List.concat

            let! shuffled = Gen.shuffle fragments
            return whole, List.ofArray shuffled
        }

    let allRelations = RelationSet.toList RelationSet.all

    let private ascending =
        Gen.listOfLength 4 (Gen.choose (1, 3)) |> Gen.map (List.scan (+) 0 >> List.tail)

    let private shapeOf relation (p, q, r, s) =
        match relation with
        | Before -> (p, q), (r, s)
        | After -> (r, s), (p, q)
        | Meets -> (p, q), (q, r)
        | MetBy -> (q, r), (p, q)
        | Overlaps -> (p, r), (q, s)
        | OverlappedBy -> (q, s), (p, r)
        | Starts -> (p, q), (p, r)
        | StartedBy -> (p, r), (p, q)
        | Finishes -> (q, r), (p, r)
        | FinishedBy -> (p, r), (q, r)
        | During -> (q, r), (p, s)
        | Contains -> (p, s), (q, r)
        | Equals -> (p, q), (p, q)

    /// A relation and a pair of intervals built to stand in exactly that relation.
    let relationScenario =
        gen {
            let! relation = Gen.elements allRelations
            let! points = ascending
            let! upperKind = kind

            let build (first, last) =
                bounded (inc first, { Value = last; Kind = upperKind })

            let a, b =
                match points with
                | [ p; q; r; s ] -> shapeOf relation (p, q, r, s)
                | _ -> failwith "ascending always yields four points"

            return relation, build a, build b
        }

    /// Registered through FsCheckConfig.arbitrary.
    type Arbitraries =
        static member Relation() = Arb.fromGen (Gen.elements allRelations)

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
