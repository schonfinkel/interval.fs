module Interval.Tests.Properties

open Expecto
open FsCheck

open Interval.Core
open Interval.Core.Boundary
open Interval.Builders
open Interval.Functions
open Interval.Predicates
open Interval.Tests.Generators

/// FsCheck arbitraries for the library's types, registered via Expecto's FsCheckConfig.
type CustomArb =
    static member Boundary() : Arbitrary<Boundary.Point<int>> = generateBoundary () |> Arb.fromGen

    static member BoundedInterval() : Arbitrary<BoundedInterval<int>> =
        generateBoundedInterval () |> Arb.fromGen

    static member Interval() : Arbitrary<Interval<int>> = generateInterval () |> Arb.fromGen

    static member Relation() : Arbitrary<Relation> =
        Gen.elements
            [ After
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
              StartedBy ]
        |> Arb.fromGen

let private config =
    { FsCheckConfig.defaultConfig with
        arbitrary = [ typeof<CustomArb> ] }


// Checks if a Boundary acts in consistent ways
let private boundaryProperties =
    testList
        "Boundary"
        [ testPropertyWithConfig
              config
              "compare on equal-kind boundaries matches value compare"
              (fun (v1: int) (v2: int) (included: bool) ->
                  let kind = if included then Included else Excluded
                  let b1 = { Value = v1; Kind = kind }
                  let b2 = { Value = v2; Kind = kind }
                  compare b1 b2 = compare v1 v2)

          testPropertyWithConfig config "Included > Excluded at the same value" (fun (v: int) ->
              { Value = v; Kind = Included } > { Value = v; Kind = Excluded })

          testPropertyWithConfig config "compare b b = 0" (fun (b: Boundary.Point<int>) -> compare b b = 0)

          testPropertyWithConfig config "Kind (+) is associative" (fun (a: Kind) (b: Kind) (c: Kind) ->
              (a + b) + c = a + (b + c)) ]

// Checks if BoundedInterval.TryCreate behaves properly
let private tryCreateProperties =
    testList
        "BoundedInterval.TryCreate"
        [ testPropertyWithConfig
              config
              "Start >= End is rejected"
              (fun (s: Boundary.Point<int>) (e: Boundary.Point<int>) ->
                  (s >= e)
                  ==> lazy
                      (match BoundedInterval.TryCreate(s, e) with
                       | Error(InvalidBoundaries _) -> true
                       | _ -> false))

          testPropertyWithConfig
              config
              "Start < End produces a matching record"
              (fun (s: Boundary.Point<int>) (e: Boundary.Point<int>) ->
                  (s < e) ==> lazy (BoundedInterval.TryCreate(s, e) = Ok { Start = s; End = e })) ]


/// Checks all Intersection Properties
let private intersectionProperties =
    testList
        "intersection"
        [ testPropertyWithConfig config "is symmetric" (fun (a: Interval<int>) (b: Interval<int>) ->
              intersection a b = intersection b a)

          testPropertyWithConfig config "∅ ∩ i = ∅" (fun (i: Interval<int>) -> intersection Empty i = Empty)

          testPropertyWithConfig config "i ∩ ∅ = ∅" (fun (i: Interval<int>) -> intersection i Empty = Empty) ]


/// Checks all Union Properties
let private unionProperties =
    testList
        "union"
        [ testPropertyWithConfig config "is symmetric" (fun (a: Interval<int>) (b: Interval<int>) ->
              union a b = union b a)

          testPropertyWithConfig config "∅ ∪ i = i" (fun (i: Interval<int>) -> union Empty i = i)

          testPropertyWithConfig config "i ∪ ∅ = i" (fun (i: Interval<int>) -> union i Empty = i) ]

/// Relate

let private relateProperties =
    testList
        "relate"
        [ testPropertyWithConfig config "relate i i = Equals" (fun (i: Interval<int>) -> relate i i = Equals)

          testPropertyWithConfig config "invert is an involution" (fun (r: Relation) -> invert (invert r) = r)

          testPropertyWithConfig
              config
              "relate a b = invert (relate b a) for Singleton intervals"
              (fun (a: BoundedInterval<int>) (b: BoundedInterval<int>) ->
                  relate (Singleton a) (Singleton b) = invert (relate (Singleton b) (Singleton a)))

          testPropertyWithConfig config "∅ is Contained by any non-empty interval" (fun (i: Interval<int>) ->
              isNotEmpty i ==> lazy (relate i Empty = Contains))

          testPropertyWithConfig
              config
              "∀ s1, s2, s3, s4, (s1 < e1 < s2 < e2) ⇒ [s1, e1] is Before [s2, e2]"
              (fun (a: int) (b: int) (c: int) (d: int) ->
                  let xs = [ a; b; c; d ]

                  (List.distinct xs |> List.length = 4)
                  ==> lazy
                      (let sorted = List.sort xs
                       let s1, e1, s2, e2 = sorted.[0], sorted.[1], sorted.[2], sorted.[3]
                       relate (singleton (inc s1) (inc e1)) (singleton (inc s2) (inc e2)) = Before))

          testPropertyWithConfig
              config
              "∀ s1, s2, s3, s4, (s1 < s2 < e1 < e2) ⇒ [s1, e1] Overlaps [s2, e2]"
              (fun (a: int) (b: int) (c: int) (d: int) ->
                  let xs = [ a; b; c; d ]

                  (List.distinct xs |> List.length = 4)
                  ==> lazy
                      (let sorted = List.sort xs
                       let s1, s2, e1, e2 = sorted.[0], sorted.[1], sorted.[2], sorted.[3]
                       relate (singleton (inc s1) (inc e1)) (singleton (inc s2) (inc e2)) = Overlaps))

          testPropertyWithConfig config "shared start, shorter a ⇒ Starts" (fun (a: int) (b: int) (c: int) ->
              let xs = [ a; b; c ]

              (List.distinct xs |> List.length = 3)
              ==> lazy
                  (let sorted = List.sort xs
                   let s, e1, e2 = sorted.[0], sorted.[1], sorted.[2]
                   relate (singleton (inc s) (inc e1)) (singleton (inc s) (inc e2)) = Starts))

          testPropertyWithConfig config "shared end, later-starting a ⇒ Finishes" (fun (a: int) (b: int) (c: int) ->
              let xs = [ a; b; c ]

              (List.distinct xs |> List.length = 3)
              ==> lazy
                  (let sorted = List.sort xs
                   let s1, s2, e = sorted.[0], sorted.[1], sorted.[2]
                   relate (singleton (inc s2) (inc e)) (singleton (inc s1) (inc e)) = Finishes))

          testPropertyWithConfig config "strict containment ⇒ Contains" (fun (a: int) (b: int) (c: int) (d: int) ->
              let xs = [ a; b; c; d ]

              (List.distinct xs |> List.length = 4)
              ==> lazy
                  (let sorted = List.sort xs
                   let s1, s2, e2, e1 = sorted.[0], sorted.[1], sorted.[2], sorted.[3]
                   relate (singleton (inc s1) (inc e1)) (singleton (inc s2) (inc e2)) = Contains))

          testPropertyWithConfig
              config
              "a.End.Value = b.Start.Value (both Included) ⇒ Meets"
              (fun (a: int) (b: int) (c: int) ->
                  let xs = [ a; b; c ]

                  (List.distinct xs |> List.length = 3)
                  ==> lazy
                      (let sorted = List.sort xs
                       let s, i, e = sorted.[0], sorted.[1], sorted.[2]
                       relate (singleton (inc s) (inc i)) (singleton (inc i) (inc e)) = Meets)) ]

let propertyTests =
    testList
        "[Property Tests]"
        [ boundaryProperties
          tryCreateProperties
          intersectionProperties
          unionProperties
          relateProperties ]
