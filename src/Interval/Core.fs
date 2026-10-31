namespace Interval.Core

open System

/// Why a pair of endpoints cannot form a bounded interval.
type IntervalError =
    /// The lower value is greater than the upper value.
    | InvertedBounds
    /// The values are equal but at least one side is excluded, so the interval holds nothing.
    | EmptyBounds
    /// An endpoint is not equal to itself (for example NaN), so it cannot be ordered.
    | NotComparable

    /// Human-readable description of the error, suitable for surfacing to users.
    member this.Description =
        match this with
        | InvertedBounds -> "The lower bound must not be greater than the upper bound"
        | EmptyBounds -> "Equal bounds must both be included, otherwise the interval holds nothing"
        | NotComparable -> "Bounds must be comparable values"

/// Whether an endpoint's value belongs to the interval.
type Kind =
    | Excluded
    | Included

/// An interval endpoint: a value and whether it belongs to the interval.
/// Deliberately has no ordering: the right tie-break at equal values depends on
/// whether the point is a lower or an upper bound.
[<NoComparison>]
type Point<'T when 'T: comparison> = { Value: 'T; Kind: Kind }

/// Endpoint orderings. Lower and upper bounds break ties in opposite directions.
module internal Endpoint =
    /// Orders lower bounds: at equal values the included bound starts earlier.
    let compareLower (a: Point<'T>) (b: Point<'T>) =
        match compare a.Value b.Value, a.Kind, b.Kind with
        | 0, Included, Excluded -> -1
        | 0, Excluded, Included -> 1
        | order, _, _ -> order

    /// Orders upper bounds: at equal values the excluded bound ends earlier.
    let compareUpper (a: Point<'T>) (b: Point<'T>) =
        match compare a.Value b.Value, a.Kind, b.Kind with
        | 0, Excluded, Included -> -1
        | 0, Included, Excluded -> 1
        | order, _, _ -> order

    /// Whether a lower and an upper bound enclose at least one value.
    let encloses (lower: Point<'T>) (upper: Point<'T>) =
        match compare lower.Value upper.Value, lower.Kind, upper.Kind with
        | 0, Included, Included -> true
        | order, _, _ -> order < 0

    /// The complementary bound at the same value: the end of a gap that starts here.
    let flip (point: Point<'T>) =
        { point with
            Kind =
                match point.Kind with
                | Included -> Excluded
                | Excluded -> Included }

/// A connected, non-empty range of values. Only constructible through
/// TryCreate, so every instance encloses at least one value.
[<CustomEquality; CustomComparison; StructuredFormatDisplay("{Display}")>]
type BoundedInterval<'T when 'T: comparison> =
    internal
        { LowerBound: Point<'T>
          UpperBound: Point<'T> }

    /// The lower endpoint.
    member this.Lower = this.LowerBound

    /// The upper endpoint.
    member this.Upper = this.UpperBound

    /// Mathematical notation, for example [1, 5).
    member this.Display =
        let opening =
            match this.LowerBound.Kind with
            | Included -> "["
            | Excluded -> "("

        let closing =
            match this.UpperBound.Kind with
            | Included -> "]"
            | Excluded -> ")"

        $"{opening}{this.LowerBound.Value}, {this.UpperBound.Value}{closing}"


    override this.Equals other =
        match other with
        | :? BoundedInterval<'T> as that -> this.LowerBound = that.LowerBound && this.UpperBound = that.UpperBound
        | _ -> false

    override this.GetHashCode() = hash (this.LowerBound, this.UpperBound)

    interface IComparable with
        /// Orders by lower bound, then by upper bound.
        member this.CompareTo other =
            match other with
            | :? BoundedInterval<'T> as that ->
                match Endpoint.compareLower this.LowerBound that.LowerBound with
                | 0 -> Endpoint.compareUpper this.UpperBound that.UpperBound
                | order -> order
            | _ -> invalidArg (nameof other) "Cannot compare intervals of different types"

    /// Builds an interval, rejecting endpoints that enclose no value.
    static member TryCreate(lower: Point<'T>, upper: Point<'T>) : Result<BoundedInterval<'T>, IntervalError> =
        let isOrdered value = value = value

        if not (isOrdered lower.Value && isOrdered upper.Value) then
            Error NotComparable
        elif Endpoint.encloses lower upper then
            Ok
                { LowerBound = lower
                  UpperBound = upper }
        elif compare lower.Value upper.Value > 0 then
            Error InvertedBounds
        else
            Error EmptyBounds

/// Algorithms over ascending arrays of pairwise disconnected intervals.
module internal Sweep =
    /// Whether two intervals form one range, given that the first starts no later.
    let connected (first: BoundedInterval<'T>) (second: BoundedInterval<'T>) =
        match compare first.UpperBound.Value second.LowerBound.Value, first.UpperBound.Kind, second.LowerBound.Kind with
        | 0, Excluded, Excluded -> false
        | order, _, _ -> order >= 0

    /// Merges connected neighbours of intervals already sorted by lower bound.
    let coalesce (sorted: BoundedInterval<'T> array) =
        let pieces = ResizeArray sorted.Length

        for item in sorted do
            if pieces.Count > 0 && connected pieces[pieces.Count - 1] item then
                let last = pieces[pieces.Count - 1]

                if Endpoint.compareUpper item.UpperBound last.UpperBound > 0 then
                    pieces[pieces.Count - 1] <-
                        { last with
                            UpperBound = item.UpperBound }
            else
                pieces.Add item

        pieces.ToArray()

    /// The canonical pieces of intervals given in any order.
    let normalize (items: BoundedInterval<'T> seq) =
        let sorted = Seq.toArray items
        Array.sortInPlace sorted
        coalesce sorted

    /// Where a value lies relative to an interval: -1 before, 0 inside, 1 after.
    let position (value: 'T) (piece: BoundedInterval<'T>) =
        let point = { Value = value; Kind = Included }

        if not (Endpoint.encloses piece.LowerBound point) then -1
        elif not (Endpoint.encloses point piece.UpperBound) then 1
        else 0

    /// The union of two canonical piece arrays, in linear time.
    let union (left: BoundedInterval<'T> array) (right: BoundedInterval<'T> array) =
        let merged = Array.zeroCreate (left.Length + right.Length)

        let rec fill i j =
            let leftIsNext =
                i < left.Length
                && (j >= right.Length
                    || Endpoint.compareLower left[i].LowerBound right[j].LowerBound <= 0)

            if leftIsNext then
                merged[i + j] <- left[i]
                fill (i + 1) j
            elif j < right.Length then
                merged[i + j] <- right[j]
                fill i (j + 1)

        fill 0 0
        coalesce merged

    /// The intersection of two canonical piece arrays, in linear time.
    let intersection (left: BoundedInterval<'T> array) (right: BoundedInterval<'T> array) =
        let pieces = ResizeArray()

        let rec scan i j =
            if i < left.Length && j < right.Length then
                let lower =
                    if Endpoint.compareLower left[i].LowerBound right[j].LowerBound >= 0 then
                        left[i].LowerBound
                    else
                        right[j].LowerBound

                let leftEndsFirst =
                    Endpoint.compareUpper left[i].UpperBound right[j].UpperBound <= 0

                let upper =
                    if leftEndsFirst then
                        left[i].UpperBound
                    else
                        right[j].UpperBound

                if Endpoint.encloses lower upper then
                    pieces.Add
                        { LowerBound = lower
                          UpperBound = upper }

                if leftEndsFirst then scan (i + 1) j else scan i (j + 1)

        scan 0 0
        pieces.ToArray()

    /// The values in the left pieces and not in the right ones, in linear time.
    let difference (left: BoundedInterval<'T> array) (right: BoundedInterval<'T> array) =
        let pieces = ResizeArray()

        let addIfNonEmpty lower upper =
            if Endpoint.encloses lower upper then
                pieces.Add
                    { LowerBound = lower
                      UpperBound = upper }

        let rec firstReaching (piece: BoundedInterval<'T>) j =
            if j < right.Length && not (Endpoint.encloses piece.LowerBound right[j].UpperBound) then
                firstReaching piece (j + 1)
            else
                j

        let rec cut (piece: BoundedInterval<'T>) lower j =
            if j < right.Length && Endpoint.encloses right[j].LowerBound piece.UpperBound then
                addIfNonEmpty lower (Endpoint.flip right[j].LowerBound)
                cut piece (Endpoint.flip right[j].UpperBound) (j + 1)
            else
                addIfNonEmpty lower piece.UpperBound

        let rec walk i j =
            if i < left.Length then
                let start = firstReaching left[i] j
                cut left[i] left[i].LowerBound start
                walk (i + 1) start

        walk 0 0
        pieces.ToArray()

/// Any finite union of bounded intervals, held in canonical form: ascending,
/// non-empty and pairwise disconnected. Equal sets of values are always equal
/// values of this type.
[<NoComparison; StructuredFormatDisplay("{Display}")>]
type Interval<'T when 'T: comparison> =
    internal
    | Pieces of BoundedInterval<'T> array

    /// Mathematical notation, for example [1, 2) ∪ (3, 4].
    member this.Display =
        match this with
        | Pieces [||] -> "∅"
        | Pieces pieces -> pieces |> Array.map _.Display |> String.concat " ∪ "


    /// The identity of union, which also lets List.sum work on intervals.
    static member Zero: Interval<'T> = Pieces [||]

    /// Union.
    static member (+)(Pieces left: Interval<'T>, Pieces right: Interval<'T>) = Pieces(Sweep.union left right)

    /// Intersection.
    static member (*)(Pieces left: Interval<'T>, Pieces right: Interval<'T>) = Pieces(Sweep.intersection left right)

    /// Difference.
    static member (-)(Pieces left: Interval<'T>, Pieces right: Interval<'T>) = Pieces(Sweep.difference left right)

/// Read access to the canonical form of an interval.
[<AutoOpen>]
module Patterns =
    /// Splits an interval into nothing, one connected piece, or several ascending pieces.
    let (|Empty|Singleton|Union|) (Pieces pieces: Interval<'T>) =
        match pieces with
        | [||] -> Empty
        | [| piece |] -> Singleton piece
        | many -> Union(List.ofArray many)
