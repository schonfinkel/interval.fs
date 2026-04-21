module Interval.Tests.TryCreate

open Expecto
open Expecto.Flip

open Interval.Builders
open Interval.Core

let private startGreaterThanEndIsRejected () =
    BoundedInterval.TryCreate(inc 5, inc 1)
    |> function
        | Error(InvalidBoundaries _) -> ()
        | other -> failtestf "Expected InvalidBoundaries, got %A" other

let private startEqualsEndWithBothIncludedIsRejected () =
    // Start >= End is rejected regardless of Kind, a point-width interval
    // with both Included still has Start = End, which `(>=)` considers invalid.
    BoundedInterval.TryCreate(inc 1, inc 1)
    |> function
        | Error(InvalidBoundaries _) -> ()
        | other -> failtestf "Expected InvalidBoundaries, got %A" other

let private validInputRoundTrips () =
    let s = exc 1
    let e = inc 5

    BoundedInterval.TryCreate(s, e)
    |> Expect.equal "Valid boundaries round-trip to a direct record" (Ok(bounded s e))

let private errorCarriesDescription () =
    match BoundedInterval.TryCreate(inc 10, exc 2) with
    | Error err -> Expect.isNonEmpty "Error description should not be empty" err.Description
    | Ok _ -> failtest "Expected Error"

let unitTests =
    testList
        "[Unit Tests] BoundedInterval.TryCreate"
        [ testCase "Start > End is rejected" startGreaterThanEndIsRejected
          testCase "Start = End is rejected" startEqualsEndWithBothIncludedIsRejected
          testCase "Valid input round-trips" validInputRoundTrips
          testCase "Error carries a description" errorCarriesDescription ]
