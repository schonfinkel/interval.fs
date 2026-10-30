namespace Interval.Next

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
