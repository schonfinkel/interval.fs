/// Worked examples that pin the documented behaviour of the public API.
module Interval.Tests.Examples

open Expecto

open Interval.Builders
open Interval.Core

let private bounded lower upper =
    match BoundedInterval.TryCreate(lower, upper) with
    | Ok interval -> interval
    | Error error -> failtest error.Description

let private rejects expected (lower, upper) =
    Expect.equal (BoundedInterval.TryCreate(lower, upper)) (Error expected) "Should reject these endpoints"

let private tryCreate =
    testList
        "TryCreate"
        [ test "inverted bounds are rejected" { rejects InvertedBounds (inc 5, inc 1) }
          test "a closed point is valid" { Expect.equal (bounded (inc 1) (inc 1)).Upper (inc 1) "Should be [1,1]" }
          test "an open-closed point holds nothing" { rejects EmptyBounds (exc 1, inc 1) }
          test "a closed-open point holds nothing" { rejects EmptyBounds (inc 1, exc 1) }
          test "an open point holds nothing" { rejects EmptyBounds (exc 1, exc 1) }
          test "NaN is rejected" { rejects NotComparable (inc nan, inc 1.0) }
          test "errors describe themselves" { Expect.isNotEmpty InvertedBounds.Description "Should have text" } ]

let private builders =
    testList
        "builders"
        [ test "an inverted range is empty" { Expect.equal (closed 5 1) Interval.empty "Should be empty" }
          test "an open point is empty" { Expect.equal (opened 1 1) Interval.empty "Should be empty" }
          test "a point holds its value" { Expect.isTrue (Interval.contains 3 (point 3)) "Should hold 3" } ]

let private operations =
    testList
        "operations"
        [ test "(1,5) ∩ [3,7] = [3,5)" {
              Expect.equal (Interval.intersection (opened 1 5) (closed 3 7)) (closedOpen 3 5) "Intersection"
          }
          test "(6,10] ∩ [5,12] = (6,10]" {
              Expect.equal (Interval.intersection (openClosed 6 10) (closed 5 12)) (openClosed 6 10) "Sub-interval"
          }
          test "(6,10] ∩ [11,12] = ∅" {
              Expect.equal (Interval.intersection (openClosed 6 10) (closed 11 12)) Interval.empty "Disjoint"
          }
          test "(1,5) ∪ [3,7] = (1,7]" {
              Expect.equal (Interval.union (opened 1 5) (closed 3 7)) (openClosed 1 7) "Union"
          }
          test "disjoint union keeps both pieces in order" {
              Expect.equal
                  (Interval.toList (Interval.union (closed 5 7) (opened 1 4)))
                  [ bounded (exc 1) (exc 4); bounded (inc 5) (inc 7) ]
                  "Pieces"
          }
          test "[1,10] − [3,5) = [1,3) ∪ [5,10]" {
              Expect.equal
                  (Interval.difference (closed 1 10) (closedOpen 3 5))
                  (Interval.union (closedOpen 1 3) (closed 5 10))
                  "Difference"
          }
          test "ofSeq collapses the docs example" {
              let merged =
                  Interval.ofSeq
                      [ bounded (exc 1) (exc 5)
                        bounded (inc 3) (inc 7)
                        bounded (exc 6) (inc 8)
                        bounded (exc 6) (inc 10)
                        bounded (inc 11) (inc 12) ]

              Expect.equal merged (Interval.union (openClosed 1 10) (closed 11 12)) "Merged"
          }
          test "operators and List.sum follow the named functions" {
              let a, b = closed 1 4, closed 3 7
              Expect.equal (a + b) (Interval.union a b) "Plus"
              Expect.equal (a * b) (Interval.intersection a b) "Times"
              Expect.equal (a - b) (Interval.difference a b) "Minus"
              Expect.equal (List.sum [ a; b ]) (closed 1 7) "Sum"
          }
          test "patterns expose the canonical form" {
              let describe interval =
                  match interval with
                  | Empty -> 0
                  | Singleton _ -> 1
                  | Union pieces -> pieces.Length

              Expect.equal
                  (List.map describe [ Interval.empty; closed 1 2; closed 1 2 + closed 4 5 ])
                  [ 0; 1; 2 ]
                  "Cases"
          }
          test "hull spans the gaps" { Expect.equal (Interval.hull (closed 1 2 + opened 4 5)) (closedOpen 1 5) "Hull" }
          test "mapMonotonic shifts endpoints" {
              Expect.equal (Interval.mapMonotonic ((+) 10) (closedOpen 1 2)) (closedOpen 11 12) "Mapped"
          }
          test "tryRelate needs connected operands" {
              Expect.equal (Interval.tryRelate (closed 1 2) (closed 3 4)) (Some Before) "Connected"
              Expect.isNone (Interval.tryRelate (closed 1 2 + closed 3 4) (closed 5 6)) "Union"
              Expect.isNone (Interval.tryRelate Interval.empty (closed 5 6)) "Empty"
          } ]

let private carriers =
    testList
        "other value types"
        [ test "float" {
              Expect.isTrue (Interval.contains 1.5 (closedOpen 1.0 2.0)) "Inside"
              Expect.isFalse (Interval.contains 2.0 (closedOpen 1.0 2.0)) "Excluded end"
          }
          test "string" { Expect.equal (Interval.union (closed "a" "c") (closed "b" "d")) (closed "a" "d") "Union" }
          test "DateTimeOffset" {
              let at (day: int) =
                  System.DateTimeOffset(2026, 1, day, 0, 0, 0, System.TimeSpan.Zero)

              Expect.equal
                  (Interval.intersection (closedOpen (at 1) (at 5)) (closedOpen (at 3) (at 9)))
                  (closedOpen (at 3) (at 5))
                  "Overlap"
          } ]

let private relations =
    let cases =
        [ Equals, (1, 5), (1, 5)
          Before, (1, 3), (5, 7)
          After, (5, 7), (1, 3)
          Meets, (1, 3), (3, 5)
          MetBy, (3, 5), (1, 3)
          Overlaps, (1, 4), (3, 6)
          OverlappedBy, (3, 6), (1, 4)
          Starts, (1, 3), (1, 5)
          StartedBy, (1, 5), (1, 3)
          Finishes, (3, 5), (1, 5)
          FinishedBy, (1, 5), (3, 5)
          During, (3, 5), (1, 7)
          Contains, (1, 7), (3, 5) ]

    testList
        "Allen's 13 relations"
        [ for expected, (a1, a2), (b1, b2) in cases ->
              test $"%A{expected}" {
                  let a, b = bounded (inc a1) (inc a2), bounded (inc b1) (inc b2)
                  Expect.equal (BoundedInterval.relate a b) expected "Relation"
                  Expect.isTrue (RelationSet.holds (RelationSet.singleton expected) a b) "Holds"
              } ]

let tests =
    testList "[Examples]" [ tryCreate; builders; operations; carriers; relations ]
