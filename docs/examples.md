# Examples

This page walks through every public operation in `interval.fs` with runnable examples. Every snippet uses the integer type for clarity; the library is generic over any `'T` that implements `IEquatable` and `IComparable`.

- [Examples](#examples)
  - [Setup](#setup)
  - [Boundaries](#boundaries)
  - [Bounded intervals](#bounded-intervals)
    - [Safe construction with `TryCreate`](#safe-construction-with-trycreate)
    - [Direct record construction](#direct-record-construction)
  - [Intervals](#intervals)
    - [`Interval.ofSet` smart constructor](#intervalofset-smart-constructor)
  - [Operations](#operations)
    - [Intersection](#intersection)
    - [Union](#union)
    - [Merge](#merge)
    - [Relationships](#relationships)
    - [Invert](#invert)
  - [Error model](#error-model)

---

## Setup

```fsharp
open Interval.Core
open Interval.Functions
```

`Interval.Core` exposes the types, `Interval.Functions` exposes the operations.

---

## Boundaries

A `Boundary<'T>` is a value plus an open/closed `BoundaryKind`:

```fsharp
// Included, the value is part of the interval, e.g. the `1` in [1, 5)
let a = { Value = 1; Kind = Included }

// Excluded, the value is not part of the interval, e.g. the `5` in [1, 5)
let b = { Value = 5; Kind = Excluded }
```

Boundaries compare by value first, then by kind (`Included` ranks above `Excluded` at the same value). This is what makes `max`/`min` of boundaries work correctly when the library computes intersections and unions.

---

## Bounded intervals

A `BoundedInterval<'T>` is the pair `(Start, End)`. The library has two paths to build one.

### Safe construction with `TryCreate`

`BoundedInterval.TryCreate` is the recommended entry point. It rejects any combination where `Start >= End`, so you can never end up with an inverted or zero-width range silently propagating through `union` / `intersection`:

```fsharp
let startB = { Value = 1; Kind = Included }
let endB   = { Value = 5; Kind = Excluded }

match BoundedInterval.TryCreate(startB, endB) with
| Ok interval ->
    // interval : BoundedInterval<int>
    printfn "valid: %A" interval
| Error (InvalidBoundaries reason) ->
    printfn "rejected: %s" reason
| Error (InvalidArgument msg) ->
    printfn "rejected: %s" msg
```

Inverted input is rejected:

```fsharp
BoundedInterval.TryCreate({ Value = 5; Kind = Included }, { Value = 1; Kind = Included })
// Error (InvalidBoundaries "Start must be strictly less than End")
```

### Direct record construction

The `BoundedInterval<'T>` record constructor is still public for backwards compatibility. It compiles but does **not** validate, use it only when you've already proven the invariant some other way.

```fsharp
// Compiles. Runtime behaviour of `(+)` and `(*)` on an inverted interval is undefined.
let dangerous = { Start = { Value = 5; Kind = Included }; End = { Value = 1; Kind = Included } }
```

**Prefer `TryCreate` for any input that isn't a compile-time literal.**

---

## Intervals

`Interval<'T>` is the sum of three cases:

| Case | Meaning |
|---|---|
| `Empty` | The empty set. |
| `Singleton b` | A single contiguous range. |
| `Union s` | A set of two or more pairwise disjoint ranges. |

```fsharp
let emptySet   : Interval<int> = Empty
let singleton1 : Interval<int> = Singleton { Start = { Value = 1; Kind = Included }; End = { Value = 5; Kind = Excluded } }
```

### `Interval.ofSet` smart constructor

`Interval.ofSet` collapses a `Set<BoundedInterval<'T>>` to the canonical form: 0 items → `Empty`, 1 item → `Singleton`, 2+ items → `Union`. Use it when you're assembling an interval from an arbitrary set of ranges.

```fsharp
let b1 = { Start = { Value = 1; Kind = Included }; End = { Value = 2; Kind = Included } }
let b2 = { Start = { Value = 4; Kind = Included }; End = { Value = 5; Kind = Included } }

Set.ofList [ b1; b2 ] |> Interval.ofSet  // Union { b1 ; b2 }
Set.ofList [ b1 ] |> Interval.ofSet  // Singleton b1
Set.empty<_> |> Interval.ofSet  // Empty
```

---

## Operations

All examples use this preamble:

```fsharp
let x1, y1 = { Value = 1; Kind = Excluded }, { Value = 5; Kind = Excluded }   // (1, 5)
let x2, y2 = { Value = 3; Kind = Included }, { Value = 7; Kind = Included }   // [3, 7]
let x3, y3 = { Value = 6; Kind = Excluded }, { Value = 8; Kind = Included }   // (6, 8]
let x4, y4 = { Value = 6; Kind = Excluded }, { Value = 10; Kind = Included }  // (6, 10]
let x5, y5 = { Value = 11; Kind = Included }, { Value = 12; Kind = Included } // [11, 12]

let i1 = Singleton { Start = x1; End = y1 }
let i2 = Singleton { Start = x2; End = y2 }
let i3 = Singleton { Start = x3; End = y3 }
let i4 = Singleton { Start = x4; End = y4 }
let i5 = Singleton { Start = x5; End = y5 }
```

### Intersection

```fsharp
// (1, 5) ∩ [3, 7]  →  [3, 5)
intersection i1 i2
// Singleton { Start = { Value = 3; Kind = Included }; End = { Value = 5; Kind = Excluded } }

// [3, 7] ∩ (6, 8]  →  (6, 7]
intersection i2 i3
// Singleton { Start = { Value = 6; Kind = Excluded }; End = { Value = 7; Kind = Included } }

// Disjoint intervals → Empty
intersection i1 i3
// Empty
```

### Union

```fsharp
// (1, 5) ∪ [3, 7]  →  (1, 7]
union i1 i2
// Singleton { Start = { Value = 1; Kind = Excluded }; End = { Value = 7; Kind = Included } }

// [3, 7] ∪ (6, 8]  →  [3, 8]
union i2 i3

// Disjoint union keeps both ranges
union i1 i3
// Union (set [ { Start = (1, Excluded); End = (5, Excluded) };
//              { Start = (6, Excluded); End = (8, Included) } ])
```

### Merge

`merge` takes a list of `BoundedInterval` and collapses overlapping ranges into a single `Interval`:

```fsharp
let boundaries = [
    { Start = x1; End = y1 }
    { Start = x2; End = y2 }
    { Start = x3; End = y3 }
    { Start = x4; End = y4 }
    { Start = x5; End = y5 }
]

merge boundaries
// Union (set [ { Start = (1, Excluded); End = (10, Included) };
//              { Start = (11, Included); End = (12, Included) } ])
```

### Relationships

`relate a b` returns the Allen relation of `a` to `b`:

```fsharp
relate i1 i2   // Overlaps
relate i2 i3   // Overlaps
relate i1 i3   // Before
relate i3 i4   // Starts
relate i5 i4   // After
```

The full set of relations is:

```
After · Before · Contains · During · Equals
Finishes · FinishedBy · Meets · MetBy
Overlaps · OverlappedBy · Starts · StartedBy
```

### Invert

Every relation has an inverse. `invert` is an involution, applying it twice returns the original:

```fsharp
invert Before          // After
invert Starts          // StartedBy
invert (invert Meets)  // Meets
```

---

## Error model

All validating constructors return `Result<_, IntervalError>`:

```fsharp
type IntervalError =
    | InvalidBoundaries of reason: string
    | InvalidArgument of errorMessage: string
```

Every case carries a `Description` member suitable for logging or surfacing to users:

```fsharp
match BoundedInterval.TryCreate(endB, startB) with
| Ok _ -> ()
| Error err -> printfn "construction failed: %s" err.Description
```

The library does **not** use exceptions for business logic.
