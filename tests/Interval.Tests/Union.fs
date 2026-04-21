module Interval.Tests.Union

open Expecto
open Expecto.Flip

open Interval.Core
open Interval.Functions
open Interval.Builders

let private checkSimpleUnion () =
    let i1 = singleton (exc 1) (exc 5)
    let i2 = singleton (inc 3) (inc 7)

    union i1 i2
    |> Expect.equal "The union should be a single large interval" (singleton (exc 1) (inc 7))

let private checkDisjointUnion () =
    let b1 = bounded (exc 1) (exc 4)
    let b2 = bounded (inc 5) (inc 7)

    union (Singleton b1) (Singleton b2)
    |> Expect.equal "The union of two disjoint intervals preserves both" (Union(Set.ofList [ b1; b2 ]))

let unitTests =
    let description = "[Unit Tests] Union"

    testList
        description
        [ testCase "Simple union" checkSimpleUnion
          testCase "Disjoint union" checkDisjointUnion ]
