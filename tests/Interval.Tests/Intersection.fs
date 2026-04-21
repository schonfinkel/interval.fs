module Interval.Tests.Intersection

open Expecto
open Expecto.Flip

open Interval.Core
open Interval.Functions
open Interval.Builders

let private checkSimpleIntersection () =
    let i1 = singleton (exc 1) (exc 5)
    let i2 = singleton (inc 3) (inc 7)

    intersection i1 i2
    |> Expect.equal "There should be a non-empty intersection" (singleton (inc 3) (exc 5))

let private checkSubIntervalIntersection () =
    let inner = singleton (exc 6) (inc 10)
    let outer = singleton (inc 5) (inc 12)

    intersection inner outer
    |> Expect.equal "The intersection of a sub-interval should be the smaller interval" inner

let private checkEmptyIntersection () =
    let i1 = singleton (exc 6) (inc 10)
    let i2 = singleton (inc 11) (inc 12)

    intersection i1 i2
    |> Expect.equal "Disjoint intervals have an empty intersection" Empty

let unitTests =
    let description = "[Unit Tests] Intersection"

    testList
        description
        [ testCase "Intervals that meet have non-empty intersections" checkSimpleIntersection
          testCase "Sub Intervals" checkSubIntervalIntersection
          testCase "Disjoint intervals have empty intersections" checkEmptyIntersection ]
