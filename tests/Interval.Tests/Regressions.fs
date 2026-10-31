/// Minimal counterexamples of defects found in the v1 implementation.
module Interval.Tests.Regressions

open Expecto

open Interval.Builders
open Interval.Core
open Interval.Tests.Generators

let private bounded first last =
    BoundedInterval.TryCreate(inc first, inc last)
    |> Result.toOption
    |> Option.toList

let private relateOf a b = Interval.tryRelate a b

let private two = closed 5 6 + closed 8 9

let private equalities =
    [ "(1,3] ∪ [1,3] keeps the included start", Interval.union (openClosed 1 3) (closed 1 3), closed 1 3
      "(1,3] ∩ [1,3] keeps the excluded start", Interval.intersection (openClosed 1 3) (closed 1 3), openClosed 1 3
      "[1,3) ∪ [3,5] connects", Interval.union (closedOpen 1 3) (closed 3 5), closed 1 5
      "[1,3] ∩ (3,5] is empty", Interval.intersection (closed 1 3) (openClosed 3 5), Interval.empty
      "[1,3] ∩ [3,5] is the shared point", Interval.intersection (closed 1 3) (closed 3 5), point 3
      "[1,2] ∩ {[5,6],[8,9]} is empty", Interval.intersection (closed 1 2) two, Interval.empty
      "[5,9] ∩ {[5,6],[8,9]} keeps both pieces", Interval.intersection (closed 5 9) two, two
      "[5,8] ∪ {[5,6],[8,9]} coalesces", Interval.union (closed 5 8) two, closed 5 9
      "union regroups", closed 1 2 + closed 4 5 + closed 2 4, closed 1 2 + (closed 4 5 + closed 2 4)
      "ofArray merges out-of-order chains",
      ofPieces (List.concat [ bounded 1 2; bounded 5 6; bounded 2 3; bounded 6 7 ]),
      closed 1 3 + closed 5 7 ]

let private relations =
    [ "(1,5] finishes [1,5]", openClosed 1 5, closed 1 5, Some Finishes
      "(1,3) is before (3,5)", opened 1 3, opened 3 5, Some Before
      "[1,3) meets [3,5]", closedOpen 1 3, closed 3 5, Some Meets
      "unions have no relation", closed 1 2 + closed 4 5, closed 7 8 + closed 10 11, None ]

let tests =
    testList
        "[Regressions]"
        [ yield! [ for name, actual, expected in equalities -> test name { Expect.equal actual expected name } ]
          yield! [ for name, a, b, expected in relations -> test name { Expect.equal (relateOf a b) expected name } ]
          test "[1,3) ∪ (3,5] stays apart" {
              Expect.isFalse (Interval.contains 3 (Interval.union (closedOpen 1 3) (openClosed 3 5))) "3 is in neither"
          }
          test "(1,1] is rejected and [1,1] accepted" {
              Expect.isError (BoundedInterval.TryCreate(exc 1, inc 1)) "Holds nothing"
              Expect.isOk (BoundedInterval.TryCreate(inc 1, inc 1)) "Holds 1"
          } ]
