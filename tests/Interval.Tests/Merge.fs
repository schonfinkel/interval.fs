module Interval.Tests.Merge

open Expecto
open Expecto.Flip

open Interval.Core
open Interval.Functions
open Interval.Builders

let private checkExampleFromDocs () =
    let b1 = bounded (exc 1) (exc 5) // (1, 5)
    let b2 = bounded (inc 3) (inc 7) // [3, 7]
    let b3 = bounded (exc 6) (inc 8) // (6, 8]
    let b4 = bounded (exc 6) (inc 10) // (6, 10]
    let b5 = bounded (inc 11) (inc 12) // [11, 12]

    let expected =
        Union(
            Set.ofList
                [ bounded (exc 1) (inc 10) // (1, 10]
                  bounded (inc 11) (inc 12) ] // [11, 12]
        )

    merge [ b1; b2; b3; b4; b5 ]
    |> Expect.equal "merge collapses overlapping ranges and keeps disjoint ones" expected

let unitTests =
    let description = "[Unit Tests] Merge"

    testList description [ testCase "Example from docs/examples.md" checkExampleFromDocs ]
