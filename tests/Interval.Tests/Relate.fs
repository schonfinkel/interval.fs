module Interval.Tests.Relate

open Expecto
open Expecto.Flip

open Interval.Core
open Interval.Functions
open Interval.Predicates
open Interval.Builders

// Each case pins down one of Allen's 13 interval relations with a canonical
// example. Inverse pairs use mirrored intervals so a regression in one half
// surfaces immediately.

let private checkEquals () =
    let a = singleton (inc 1) (inc 5)
    let b = singleton (inc 1) (inc 5)
    relate a b |> Expect.equal "Same intervals relate as Equals" Equals
    isEqual a b |> Expect.isTrue "Also check if the Predicate works"

let private checkBefore () =
    let a = singleton (inc 1) (inc 3)
    let b = singleton (inc 5) (inc 7)
    relate a b |> Expect.equal "a ends strictly before b starts" Before
    precedes a b |> Expect.isTrue "Also check if the Predicate works"

let private checkAfter () =
    let a = singleton (inc 5) (inc 7)
    let b = singleton (inc 1) (inc 3)
    relate a b |> Expect.equal "a starts strictly after b ends" After

let private checkMeets () =
    let a = singleton (inc 1) (inc 3)
    let b = singleton (inc 3) (inc 5)
    relate a b |> Expect.equal "a.End = b.Start, touching without overlap" Meets

let private checkMetBy () =
    let a = singleton (inc 3) (inc 5)
    let b = singleton (inc 1) (inc 3)
    relate a b |> Expect.equal "b.End = a.Start, touching without overlap" MetBy

let private checkOverlaps () =
    let a = singleton (inc 1) (inc 4)
    let b = singleton (inc 3) (inc 6)
    relate a b |> Expect.equal "a begins before b and ends inside b" Overlaps

let private checkOverlappedBy () =
    let a = singleton (inc 3) (inc 6)
    let b = singleton (inc 1) (inc 4)
    relate a b |> Expect.equal "b begins before a and ends inside a" OverlappedBy

let private checkStarts () =
    let a = singleton (inc 1) (inc 3)
    let b = singleton (inc 1) (inc 5)
    relate a b |> Expect.equal "a shares start with b, ends earlier" Starts
    starts a b |> Expect.isTrue "Also check if the Predicate works"

let private checkStartedBy () =
    // [1, 5]
    let a = singleton (inc 1) (inc 5)
    // [1, 3]
    let b = singleton (inc 1) (inc 3)
    // [1, 5] is StartedBy [1, 3]
    relate a b |> Expect.equal "b shares start with a, ends earlier" StartedBy
    startedBy a b |> Expect.isTrue "Also check if the Predicate works"

let private checkFinishes () =
    let a = singleton (inc 3) (inc 5)
    let b = singleton (inc 1) (inc 5)
    relate a b |> Expect.equal "a shares end with b, starts later" Finishes
    finishes a b |> Expect.isTrue "Also check if the Predicate works"

let private checkFinishedBy () =
    let a = singleton (inc 1) (inc 5)
    let b = singleton (inc 3) (inc 5)
    relate a b |> Expect.equal "b shares end with a, starts later" FinishedBy
    finishedBy a b |> Expect.isTrue "Also check if the Predicate works"

let private checkDuring () =
    let a = singleton (inc 3) (inc 5)
    let b = singleton (inc 1) (inc 7)
    relate a b |> Expect.equal "a is strictly inside b (no shared endpoints)" During

let private checkContains () =
    let a = singleton (inc 1) (inc 7)
    let b = singleton (inc 3) (inc 5)

    relate a b
    |> Expect.equal "a strictly contains b (no shared endpoints)" Contains

let unitTests =
    testList
        "[Unit Tests] Relate (Allen's 13 relations)"
        [ testCase "Equals" checkEquals
          testCase "Before" checkBefore
          testCase "After" checkAfter
          testCase "Meets" checkMeets
          testCase "MetBy" checkMetBy
          testCase "Overlaps" checkOverlaps
          testCase "OverlappedBy" checkOverlappedBy
          testCase "Starts" checkStarts
          testCase "StartedBy" checkStartedBy
          testCase "Finishes" checkFinishes
          testCase "FinishedBy" checkFinishedBy
          testCase "During" checkDuring
          testCase "Contains" checkContains ]
