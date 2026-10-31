namespace Interval.Core

/// The thirteen relations of Allen's Interval Algebra. Each case describes
/// how an interval a relates to an interval b.
type Relation =
    /// a starts after b ends, with a gap between them.
    | After
    /// a ends before b starts, with a gap between them.
    | Before
    /// a starts earlier and ends later than b.
    | Contains
    /// a starts later and ends earlier than b.
    | During
    /// a and b have the same endpoints.
    | Equals
    /// a and b end together and a starts later.
    | Finishes
    /// a and b end together and a starts earlier.
    | FinishedBy
    /// a ends where b starts, with no gap between them.
    | Meets
    /// a starts where b ends, with no gap between them.
    | MetBy
    /// a starts first and ends inside b.
    | Overlaps
    /// b starts first and ends inside a.
    | OverlappedBy
    /// a and b start together and a ends earlier.
    | Starts
    /// a and b start together and a ends later.
    | StartedBy

/// Operations on single relations.
[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Relation =
    /// The relation of b to a, given the relation of a to b.
    let invert relation =
        match relation with
        | After -> Before
        | Before -> After
        | Contains -> During
        | During -> Contains
        | Equals -> Equals
        | Finishes -> FinishedBy
        | FinishedBy -> Finishes
        | Meets -> MetBy
        | MetBy -> Meets
        | Overlaps -> OverlappedBy
        | OverlappedBy -> Overlaps
        | Starts -> StartedBy
        | StartedBy -> Starts

/// Allen's relations between connected intervals, and predicates named after them.
[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module BoundedInterval =
    let private (|Earlier|Same|Later|) order =
        if order < 0 then Earlier
        elif order > 0 then Later
        else Same

    /// How an interval that starts and ends first relates to the other one.
    let private relateLeading (first: BoundedInterval<'T>) (second: BoundedInterval<'T>) =
        match compare first.Upper.Value second.Lower.Value, first.Upper.Kind, second.Lower.Kind with
        | 0, Excluded, Excluded -> Before
        | 0, _, _ -> Meets
        | order, _, _ when order < 0 -> Before
        | _ -> Overlaps

    /// Whether the value belongs to the interval.
    let contains (value: 'T) (interval: BoundedInterval<'T>) = Sweep.position value interval = 0

    /// The one relation that holds from a to b. Intervals that touch at a value
    /// held by at least one side meet; two excluded endpoints leave a gap.
    let relate (a: BoundedInterval<'T>) (b: BoundedInterval<'T>) =
        match Endpoint.compareLower a.Lower b.Lower, Endpoint.compareUpper a.Upper b.Upper with
        | Same, Same -> Equals
        | Same, Earlier -> Starts
        | Same, Later -> StartedBy
        | Later, Same -> Finishes
        | Earlier, Same -> FinishedBy
        | Earlier, Later -> Contains
        | Later, Earlier -> During
        | Earlier, Earlier -> relateLeading a b
        | Later, Later -> Relation.invert (relateLeading b a)

    /// a ends before b starts, with a gap between them.
    let before a b = relate a b = Before

    /// a starts after b ends, with a gap between them.
    let after a b = relate a b = After

    /// a ends where b starts, with no gap between them.
    let meets a b = relate a b = Meets

    /// a starts where b ends, with no gap between them.
    let metBy a b = relate a b = MetBy

    /// a starts first and ends inside b.
    let overlaps a b = relate a b = Overlaps

    /// b starts first and ends inside a.
    let overlappedBy a b = relate a b = OverlappedBy

    /// a and b start together and a ends earlier.
    let starts a b = relate a b = Starts

    /// a and b start together and a ends later.
    let startedBy a b = relate a b = StartedBy

    /// a and b end together and a starts later.
    let finishes a b = relate a b = Finishes

    /// a and b end together and a starts earlier.
    let finishedBy a b = relate a b = FinishedBy

    /// a starts later and ends earlier than b.
    let during a b = relate a b = During

    /// a starts earlier and ends later than b.
    let encloses a b = relate a b = Contains

    /// a and b have the same endpoints.
    let equals a b = relate a b = Equals
