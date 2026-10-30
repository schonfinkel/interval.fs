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
