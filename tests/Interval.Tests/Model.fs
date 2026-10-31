/// An independent reference model written from the specification. It shares no
/// comparison logic with the library.
module Interval.Tests.Model

open Interval.Core
open Interval.Tests.Generators

/// Covers every generated endpoint. Even probes sit on endpoints; odd probes
/// fall strictly between them, standing in for a dense order.
let probes = [ -18 .. 26 ]

let holds value (piece: BoundedInterval<int>) =
    let aboveLower =
        value > piece.Lower.Value
        || (value = piece.Lower.Value && piece.Lower.Kind = Included)

    let belowUpper =
        value < piece.Upper.Value
        || (value = piece.Upper.Value && piece.Upper.Kind = Included)

    aboveLower && belowUpper

/// The set of probes an interval holds.
let denote (interval: Interval<int>) =
    let pieces = piecesOf interval

    probes
    |> List.filter (fun value -> pieces |> List.exists (holds value))
    |> Set.ofList

let private lowerKey (point: Point<int>) =
    2 * point.Value + (if point.Kind = Included then 0 else 1)

let private upperKey (point: Point<int>) =
    2 * point.Value + (if point.Kind = Included then 0 else -1)

/// Every relation whose definition holds from a to b. Exactly one should.
let relationsHolding (a: BoundedInterval<int>) (b: BoundedInterval<int>) =
    let lowerA, upperA = lowerKey a.Lower, upperKey a.Upper
    let lowerB, upperB = lowerKey b.Lower, upperKey b.Upper

    let shape (first: BoundedInterval<int>) (second: BoundedInterval<int>) =
        let bothExcluded = first.Upper.Kind = Excluded && second.Lower.Kind = Excluded

        let gap =
            first.Upper.Value < second.Lower.Value
            || (first.Upper.Value = second.Lower.Value && bothExcluded)

        let touch = first.Upper.Value = second.Lower.Value && not bothExcluded
        gap, touch, not gap && not touch

    let gapAB, touchAB, crossAB = shape a b
    let gapBA, touchBA, crossBA = shape b a
    let aLeads = lowerA < lowerB && upperA < upperB
    let bLeads = lowerB < lowerA && upperB < upperA

    [ Before, aLeads && gapAB
      Meets, aLeads && touchAB
      Overlaps, aLeads && crossAB
      After, bLeads && gapBA
      MetBy, bLeads && touchBA
      OverlappedBy, bLeads && crossBA
      Equals, lowerA = lowerB && upperA = upperB
      Starts, lowerA = lowerB && upperA < upperB
      StartedBy, lowerA = lowerB && upperA > upperB
      Finishes, lowerA > lowerB && upperA = upperB
      FinishedBy, lowerA < lowerB && upperA = upperB
      During, lowerA > lowerB && upperA < upperB
      Contains, lowerA < lowerB && upperA > upperB ]
    |> List.filter snd
    |> List.map fst
