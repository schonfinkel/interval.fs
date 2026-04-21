namespace Interval.Core

/// <summary>
/// Errors that can be surfaced by interval construction.
/// </summary>
type IntervalError =
    /// <summary>
    /// The supplied boundaries do not form a valid interval
    /// (e.g. start is not strictly less than end).
    /// </summary>
    | InvalidBoundaries of reason: string
    /// <summary>
    /// A supplied argument was invalid for the requested operation.
    /// </summary>
    | InvalidArgument of errorMessage: string

    /// <summary>Human-readable description of the error, suitable for surfacing to users.</summary>
    member this.Description =
        match this with
        | InvalidBoundaries reason -> reason
        | InvalidArgument msg -> msg

/// <summary>
/// Building blocks for interval endpoints: the inclusion <see cref="Boundary.Kind"/>
/// and the value-plus-kind pair <see cref="Boundary.Point{T}"/> used as an
/// interval's start or end.
/// </summary>
module Boundary =
    /// <summary>
    /// Describes whether a boundary value is part of the interval.
    /// </summary>
    /// <remarks>
    /// Case order matters: <c>Excluded</c> is declared before <c>Included</c>
    /// so structural comparison orders <c>Excluded &lt; Included</c>, which is
    /// what <see cref="Point{T}"/>'s tie-breaker relies on.
    /// </remarks>
    type Kind =
        /// <summary>The boundary value is not part of the interval (open side).</summary>
        | Excluded
        /// <summary>The boundary value is part of the interval (closed side).</summary>
        | Included

        /// <summary>
        /// Combines two <see cref="Kind"/>s, favoring inclusion: the result is
        /// <c>Included</c> if either operand is <c>Included</c>, otherwise <c>Excluded</c>.
        /// </summary>
        /// <remarks>
        /// Forms an idempotent, commutative, associative monoid with <c>Excluded</c> as identity,
        /// useful when merging boundaries at the same value (the union is closed if either side is).
        /// </remarks>
        static member (+)(a: Kind, b: Kind) =
            match a, b with
            | Included, _ -> Included
            | _, Included -> Included
            | Excluded, Excluded -> Excluded

    /// <summary>
    /// A single boundary point of an interval: a value paired with whether that
    /// value is itself part of the interval. Field order (Value, Kind) drives
    /// structural comparison, values are compared first, Kind breaks ties.
    /// </summary>
    type Point<'T when 'T: equality and 'T: comparison> =
        {
            /// <summary>The underlying value at which this boundary sits.</summary>
            Value: 'T
            /// <summary>Whether <see cref="Value"/> itself belongs to the interval.</summary>
            Kind: Kind
        }

/// <summary>
/// Represents a bounded interval with start and end boundaries of type 'T.
/// </summary>
/// <remarks>
/// Invariant (enforced by <see cref="TryCreate"/>): <c>Start &lt; End</c>.
/// Constructing a record literal directly bypasses this check, prefer the
/// smart constructor at API boundaries.
/// </remarks>
type BoundedInterval<'T when 'T: equality and 'T: comparison> =
    {
        /// <summary>The lower (left) boundary of the interval.</summary>
        Start: Boundary.Point<'T>
        /// <summary>The upper (right) boundary of the interval.</summary>
        End: Boundary.Point<'T>
    }

    /// <summary>
    /// Constructs a <see cref="BoundedInterval{T}"/> after validating that
    /// <paramref name="start"/> is strictly less than <paramref name="end'"/>.
    /// </summary>
    /// <returns>
    /// <c>Ok</c> carrying the interval, or <c>Error</c> with
    /// <see cref="IntervalError.InvalidBoundaries"/> when the check fails.
    /// </returns>
    static member TryCreate(start, end') : Result<BoundedInterval<'T>, IntervalError> =
        if start >= end' then
            Error(InvalidBoundaries "Start must be strictly lesser than End")
        else
            Ok { Start = start; End = end' }

    /// <summary>
    /// Union of two intervals. Produces a <see cref="Interval.Singleton"/>
    /// when the operands overlap or touch, and a <see cref="Interval.Union"/>
    /// of both disjoint pieces otherwise.
    /// </summary>
    static member (+)(a: BoundedInterval<'T>, b: BoundedInterval<'T>) =
        let minStart = min a.Start b.Start
        let maxStart = max a.Start b.Start
        let minEnd = min a.End b.End
        let maxEnd = max a.End b.End

        if minEnd < maxStart then
            let set =
                [ { Start = minStart; End = minEnd }; { Start = maxStart; End = maxEnd } ]
                |> Set.ofList

            Union set
        else
            Singleton { Start = minStart; End = maxEnd }

    /// <summary>
    /// Intersection of two intervals. Returns the overlapping
    /// <see cref="Interval.Singleton"/> when one exists, or
    /// <see cref="Interval.Empty"/> when the operands are disjoint.
    /// </summary>
    static member (*)(a: BoundedInterval<'T>, b: BoundedInterval<'T>) =
        let newStart = max a.Start b.Start
        let newEnd = min a.End b.End

        if newStart < newEnd then
            Singleton { Start = newStart; End = newEnd }
        else
            Empty

/// <summary>
/// A possibly-disconnected interval over values of type <typeparamref name="T"/>.
/// Results of <see cref="BoundedInterval{T}"/> operations land here because
/// union and intersection are not closed over single bounded intervals.
/// </summary>
and Interval<'T when 'T: equality and 'T: comparison> =
    /// <summary>The empty interval, containing no values.</summary>
    | Empty
    /// <summary>A single contiguous bounded interval.</summary>
    | Singleton of BoundedInterval<'T>
    /// <summary>Two or more pairwise-disjoint bounded intervals.</summary>
    | Union of BoundedInterval<'T> Set

/// <summary>
/// Smart constructors and helpers for <see cref="Interval{T}"/>.
/// </summary>
[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Interval =
    /// <summary>
    /// Builds an <see cref="Interval{T}"/> from a set of bounded intervals,
    /// collapsing to <c>Empty</c> / <c>Singleton</c> when the set has 0 or 1 elements.
    /// </summary>
    let ofSet (items: BoundedInterval<'T> Set) : Interval<'T> =
        match Set.count items with
        | 0 -> Empty
        | 1 -> Singleton(Set.minElement items)
        | _ -> Union items

/// <summary>
/// The thirteen mutually exclusive relations between two intervals,
/// as defined by Allen's Interval Algebra. Cases come in inverse pairs
/// (e.g. <c>Before</c> / <c>After</c>), with <c>Equals</c> as its own inverse.
/// </summary>
/// <remarks>
/// Given intervals <c>a</c> and <c>b</c>, each case below describes the
/// relation of <c>a</c> to <c>b</c>.
/// </remarks>
type Relation =
    /// <summary><c>a</c> starts strictly after <c>b</c> ends (inverse of <see cref="Before"/>).</summary>
    | After
    /// <summary><c>a</c> ends strictly before <c>b</c> starts (they do not touch).</summary>
    | Before
    /// <summary><c>a</c> strictly contains <c>b</c>: <c>a.Start &lt; b.Start</c> and <c>b.End &lt; a.End</c>.</summary>
    | Contains
    /// <summary><c>a</c> is strictly inside <c>b</c> (inverse of <see cref="Contains"/>).</summary>
    | During
    /// <summary><c>a</c> and <c>b</c> share both endpoints.</summary>
    | Equals
    /// <summary><c>a</c> and <c>b</c> share an end, with <c>a</c> starting later (inverse of <see cref="FinishedBy"/>).</summary>
    | Finishes
    /// <summary><c>a</c> and <c>b</c> share an end, with <c>b</c> starting later.</summary>
    | FinishedBy
    /// <summary><c>a</c>'s end equals <c>b</c>'s start (they touch at one point).</summary>
    | Meets
    /// <summary><c>b</c>'s end equals <c>a</c>'s start (inverse of <see cref="Meets"/>).</summary>
    | MetBy
    /// <summary><c>a</c> begins before <c>b</c> and ends inside <c>b</c>.</summary>
    | Overlaps
    /// <summary>Inverse of <see cref="Overlaps"/>: <c>b</c> begins before <c>a</c> and ends inside <c>a</c>.</summary>
    | OverlappedBy
    /// <summary><c>a</c> and <c>b</c> share a start, with <c>a</c> ending earlier (inverse of <see cref="StartedBy"/>).</summary>
    | Starts
    /// <summary><c>a</c> and <c>b</c> share a start, with <c>b</c> ending earlier.</summary>
    | StartedBy
