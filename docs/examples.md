# Examples

Every snippet uses integers for clarity. The library is generic over any `'T` that supports F# comparison.

- [Setup](#setup)
- [Endpoints and bounded intervals](#endpoints-and-bounded-intervals)
- [Intervals](#intervals)
- [Set operations](#set-operations)
- [Allen's relations](#allens-relations)
- [Relation sets and composition](#relation-sets-and-composition)
- [Laws](#laws)
- [Performance notes](#performance-notes)

---

## Setup

```fsharp
open Interval.Core
open Interval.Builders
```

---

## Endpoints and bounded intervals

An endpoint is a value plus whether it belongs to the interval. `inc` and `exc` build them:

```fsharp
let included = inc 1 // the 1 in [1, 5)
let excluded = exc 5 // the 5 in [1, 5)
```

`BoundedInterval.TryCreate` is the only way to build a `BoundedInterval`. Every instance encloses at least one value:

```fsharp
let valid = BoundedInterval.TryCreate(inc 1, exc 5) // Ok [1, 5)
let singleValue = BoundedInterval.TryCreate(inc 3, inc 3) // Ok [3, 3]
let inverted = BoundedInterval.TryCreate(inc 5, inc 1) // Error InvertedBounds
let holdsNothing = BoundedInterval.TryCreate(exc 1, inc 1) // Error EmptyBounds
let notOrdered = BoundedInterval.TryCreate(inc nan, inc 1.0) // Error NotComparable

match valid with
| Ok interval -> printfn $"{interval.Lower.Value} to {interval.Upper.Value}"
| Error error -> printfn $"{error.Description}"
```

---

## Intervals

An `Interval<'T>` is any finite union of bounded intervals, always held in canonical form: ascending, non-empty, pairwise disconnected. Two intervals holding the same values are always equal.

Builders are total. Endpoints that enclose nothing give the empty interval:

```fsharp
let a = closed 1 5 // [1, 5]
let b = opened 1 5 // (1, 5)
let c = closedOpen 1 5 // [1, 5)
let d = openClosed 1 5 // (1, 5]
let e = point 3 // [3, 3]
let none = closed 5 1 // ∅
```

Read the canonical form with `Interval.toArray` or the `Empty` / `Singleton` / `Union` pattern:

```fsharp
let describe interval =
    match interval with
    | Empty -> "nothing"
    | Singleton piece -> $"one piece: {piece}"
    | Union pieces -> $"{pieces.Length} pieces"

let pieces = Interval.toArray (closed 1 2 + closed 4 5)
let held = Interval.contains 3 (closed 1 5) // true
```

`Interval.ofArray` builds one from bounded intervals in any order, overlapping or not:

```fsharp
let bounded lower upper =
    match BoundedInterval.TryCreate(lower, upper) with
    | Ok interval -> [| interval |]
    | Error _ -> [||]

let merged =
    Array.concat
        [ bounded (exc 1) (exc 5)
          bounded (inc 3) (inc 7)
          bounded (exc 6) (inc 10)
          bounded (inc 11) (inc 12) ]
    |> Interval.ofArray // (1, 10] ∪ [11, 12]
```

---

## Set operations

```fsharp
let i1, i2 = opened 1 5, closed 3 7

let meet = Interval.intersection i1 i2 // [3, 5)
let join = Interval.union i1 i2 // (1, 7]
let rest = Interval.difference (closed 1 10) (closedOpen 3 5) // [1, 3) ∪ [5, 10]
let span = Interval.hull (closed 1 2 + opened 4 5) // [1, 5)
let isInside = Interval.isSubsetOf (closed 2 3) (closed 1 5) // true
let shifted = Interval.mapMonotonic ((+) 10) (closedOpen 1 2) // [11, 12)
```

The same operations are available as operators, and `Array.sum` works because `∅` is the identity of union:

```fsharp
let viaOperators = (i1 + i2) * closed 2 4 - point 3
let total = Array.sum [| i1; i2 |]
let all = Interval.unions [| i1; i2 |]
let common = Interval.intersections i1 [| i2 |]
```

Touching follows set semantics, which depends on the boundaries:

```fsharp
let connected = Interval.union (closedOpen 1 3) (closed 3 5) // [1, 5]
let gapAtThree = Interval.union (closedOpen 1 3) (openClosed 3 5) // [1, 3) ∪ (3, 5]
let sharedPoint = Interval.intersection (closed 1 3) (closed 3 5) // [3, 3]
```

Values are treated as a dense order, so `[1, 2]` and `[3, 4]` over integers stay separate.

---

## Allen's relations

The thirteen relations are defined between connected intervals:

```fsharp
let pair lower upper =
    match BoundedInterval.TryCreate(inc lower, inc upper) with
    | Ok interval -> interval
    | Error error -> invalidOp error.Description

let first, second = pair 1 4, pair 3 6

let relation = BoundedInterval.relate first second // Overlaps
let inverse = Relation.invert relation // OverlappedBy
let isOverlap = BoundedInterval.overlaps first second // true
let lifted = Interval.tryRelate (closed 1 4) (closed 3 6) // Some Overlaps
let undefined = Interval.tryRelate (closed 1 2 + closed 4 5) (closed 3 6) // None
```

`relate` is decided from endpoints alone:

- Intervals that touch at a value held by at least one side **meet**: `[1, 3]` and `[3, 5]`, or `[1, 3)` and `[3, 5]`.
- Two excluded endpoints leave a gap, so `(1, 3)` is **before** `(3, 5)`.
- A point follows the same rules: `[3, 3]` **starts** `[3, 5]`, and `[1, 3]` is **finished by** `[3, 3]`.

---

## Relation sets and composition

A `RelationSet` expresses that any of several relations may hold:

```fsharp
let overlapping = RelationSet.ofArray [| Overlaps; OverlappedBy |]
let anyOverlap = RelationSet.holds overlapping first second // true
let concurrent = RelationSet.holds RelationSet.concur first second // true
let notConcurrent = RelationSet.complement RelationSet.concur = RelationSet.disjoint // true
```

Composition infers what can hold from `a` to `c`, given `a` to `b` and `b` to `c`:

```fsharp
let inferred = RelationSet.composeRelations Before Before // only Before
let general = RelationSet.compose overlapping (RelationSet.singleton Before)
let members = RelationSet.toArray inferred
```

**Soundness domain.** Composition describes proper intervals (lower value strictly below upper) with one uniform boundary shape, such as all closed or all half-open. It does not hold across mixed boundary kinds. For example `[1, 3]` meets `[3, 5]` and `[3, 5]` overlaps `(3, 6]`, so composition gives only `Before`, yet `[1, 3]` meets `(3, 6]`.

---

## Laws

Under the canonical equality, with `contains` as the observation of what an interval means:

- `∪` and `∩` are commutative, associative and idempotent, absorb each other and distribute over each other, with `∅` as the identity of `∪` and the zero of `∩`. There is no top element because intervals are bounded.
- `contains x (a ∪ b) = contains x a || contains x b`, and likewise `∩` with `&&`, and `difference` with `&& not`.
- `a ⊆ b ⇔ a ∩ b = a ⇔ a ∪ b = b`.
- `(a − b) ∪ (a ∩ b) = a` and `(a − b) ∩ b = ∅`.
- `relate a b = invert (relate b a)`, and exactly one relation holds for any pair.
- `RelationSet` is a boolean algebra. `compose` is associative, has `Equals` as identity, distributes over union and satisfies `converse (compose r s) = compose (converse s) (converse r)`.

All of these are checked by FsCheck properties in `tests/Interval.Tests`.

---

## Performance notes

- `union`, `intersection` and `difference` are linear in the number of pieces. `contains` is logarithmic. `ofArray` is `O(n log n)`.
- `ofArray` sorts in parallel from 10,000 items, and `intersections` reduces in parallel from 256 inputs. A value type's comparison must therefore be safe to call from several threads, which holds for ordinary comparison implementations.
- `Interval.mapMonotonic` runs sequentially, because it calls your function.

