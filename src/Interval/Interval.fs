namespace Interval.Core

/// Set operations on intervals. All results are canonical.
[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Interval =
    /// The interval holding no values.
    let empty<'T when 'T: comparison> : Interval<'T> = Pieces [||]

    /// An interval of exactly one connected piece.
    let ofBounded (bounded: BoundedInterval<'T>) = Pieces [| bounded |]

    /// The union of bounded intervals given in any order, overlapping or not.
    let ofSeq (items: BoundedInterval<'T> seq) = Pieces(Sweep.normalize items)

    /// The connected pieces, in ascending order.
    let toList (Pieces pieces: Interval<'T>) = List.ofArray pieces

    /// Whether the interval holds no values.
    let isEmpty (Pieces pieces: Interval<'T>) = Array.isEmpty pieces

    /// Whether the value belongs to the interval.
    let contains (value: 'T) (Pieces pieces: Interval<'T>) =
        let rec search low high =
            if low > high then
                false
            else
                let middle = low + (high - low) / 2

                match Sweep.position value pieces[middle] with
                | 0 -> true
                | -1 -> search low (middle - 1)
                | _ -> search (middle + 1) high

        search 0 (pieces.Length - 1)

    /// The values in either interval.
    let union (Pieces left: Interval<'T>) (Pieces right: Interval<'T>) = Pieces(Sweep.union left right)

    /// The values in any of the intervals.
    let unions (items: Interval<'T> seq) = items |> Seq.collect toList |> ofSeq

    /// The values in both intervals.
    let intersection (Pieces left: Interval<'T>) (Pieces right: Interval<'T>) = Pieces(Sweep.intersection left right)

    /// The values in every interval. Takes a first interval because intervals
    /// have no universal element for an empty collection to reduce to.
    let intersections (first: Interval<'T>) (rest: Interval<'T> seq) = Seq.fold intersection first rest

    /// The values in the first interval and not in the second.
    let difference (Pieces left: Interval<'T>) (Pieces right: Interval<'T>) = Pieces(Sweep.difference left right)

    /// Whether every value of the first interval belongs to the second.
    let isSubsetOf (subset: Interval<'T>) (superset: Interval<'T>) = isEmpty (difference subset superset)

    /// Whether the interval is empty or a single connected piece.
    let isConnected (Pieces pieces: Interval<'T>) = pieces.Length <= 1

    /// The smallest connected interval containing every value of the input.
    let hull (Pieces pieces: Interval<'T>) =
        match pieces with
        | [||] -> Pieces [||]
        | _ ->
            Pieces
                [| { LowerBound = pieces[0].LowerBound
                     UpperBound = pieces[pieces.Length - 1].UpperBound } |]

    /// Applies a strictly increasing function to every endpoint. Pieces whose
    /// images enclose nothing are dropped, so the result stays canonical even
    /// when the function is not strictly increasing.
    let mapMonotonic (mapping: 'T -> 'U) (Pieces pieces: Interval<'T>) =
        let mapPoint (point: Point<'T>) =
            { Value = mapping point.Value
              Kind = point.Kind }

        pieces
        |> Seq.choose (fun piece ->
            BoundedInterval.TryCreate(mapPoint piece.LowerBound, mapPoint piece.UpperBound)
            |> Result.toOption)
        |> ofSeq

    /// The Allen relation of two connected, non-empty intervals; None otherwise,
    /// because the relations are only defined for connected intervals.
    let tryRelate (Pieces left: Interval<'T>) (Pieces right: Interval<'T>) =
        match left, right with
        | [| a |], [| b |] -> Some(BoundedInterval.relate a b)
        | _ -> None
