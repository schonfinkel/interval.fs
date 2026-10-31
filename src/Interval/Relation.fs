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
        match Endpoint.order first.Upper.Value second.Lower.Value, first.Upper.Kind, second.Lower.Kind with
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

/// A set of Allen relations, for expressing that any of several relations may hold.
[<Struct>]
type RelationSet = internal Bits of bits: uint16

/// Boolean algebra, converse and composition over sets of relations.
[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module RelationSet =
    let private relations =
        [| After
           Before
           Contains
           During
           Equals
           Finishes
           FinishedBy
           Meets
           MetBy
           Overlaps
           OverlappedBy
           Starts
           StartedBy |]

    let private index relation =
        match relation with
        | After -> 0
        | Before -> 1
        | Contains -> 2
        | During -> 3
        | Equals -> 4
        | Finishes -> 5
        | FinishedBy -> 6
        | Meets -> 7
        | MetBy -> 8
        | Overlaps -> 9
        | OverlappedBy -> 10
        | Starts -> 11
        | StartedBy -> 12

    let private bit relation = 1us <<< index relation

    /// The set with no relations.
    let empty = Bits 0us

    /// The set with all thirteen relations.
    let all = Bits 0x1FFFus

    /// The set with one relation.
    let singleton relation = Bits(bit relation)

    /// The set with the given relations.
    let ofArray (items: Relation array) =
        Bits(items |> Array.fold (fun bits relation -> bits ||| bit relation) 0us)

    /// Whether the set includes the relation.
    let contains relation (Bits bits) = bits &&& bit relation <> 0us

    /// The relations in the set, in declaration order of Relation.
    let toArray set =
        relations |> Array.filter (fun relation -> contains relation set)

    /// The relations in either set.
    let union (Bits left) (Bits right) = Bits(left ||| right)

    /// The relations in both sets.
    let intersect (Bits left) (Bits right) = Bits(left &&& right)

    /// The relations not in the set.
    let complement (Bits bits) = Bits(~~~bits &&& 0x1FFFus)

    /// The inverse of every relation in the set.
    let converse set =
        set |> toArray |> Array.map Relation.invert |> ofArray

    /// Intervals that share no interior: Before, After, Meets and MetBy.
    let disjoint = ofArray [| Before; After; Meets; MetBy |]

    /// Intervals that have time in common: everything except disjoint.
    let concur = complement disjoint

    /// a lies inside b: Starts, During, Finishes and Equals.
    let within = ofArray [| Starts; During; Finishes; Equals |]

    /// Whether the relation from a to b is in the set.
    let holds set (a: BoundedInterval<'T>) (b: BoundedInterval<'T>) =
        contains (BoundedInterval.relate a b) set

    // Six points order three intervals in every possible way, so walking all
    // triples of closed intervals over them observes each composition exactly.
    let private compositions =
        let closed first last =
            { LowerBound = { Value = first; Kind = Included }
              UpperBound = { Value = last; Kind = Included } }

        let intervals =
            [| for first in 0..5 do
                   for last in first + 1 .. 5 -> closed first last |]

        let cells = Array.zeroCreate<uint16> (13 * 13)

        for a in intervals do
            for b in intervals do
                for c in intervals do
                    let cell =
                        index (BoundedInterval.relate a b) * 13 + index (BoundedInterval.relate b c)

                    cells[cell] <- cells[cell] ||| bit (BoundedInterval.relate a c)

        cells

    /// The relations possible from a to c, given the relation from a to b and from b to c.
    /// Sound for proper intervals with uniform boundary shape; see docs/examples.md.
    let composeRelations first second =
        Bits compositions[index first * 13 + index second]

    /// Composition of general relations: the union of compositions of their members.
    let compose first second =
        Array.allPairs (toArray first) (toArray second)
        |> Array.fold (fun result (r, s) -> union result (composeRelations r s)) empty
