/// Checks that the generators reach the cases the laws depend on, since a
/// green property proves little about classes it never exercised.
module Interval.Tests.GeneratorChecks

open Expecto
open FsCheck.FSharp

open Interval.Core
open Interval.Tests.Generators
open Interval.Tests.Laws
open Interval.Tests.Model

let private sampleSize = 5000

let private reaches name items expected =
    test name {
        let reached = items |> List.distinct |> Set.ofList
        Expect.equal reached (Set.ofList expected) $"{name} should cover every class"
    }

let private intervals = Gen.sample sampleSize interval |> List.ofArray
let private pairs = Gen.sample sampleSize (Gen.two boundedInterval) |> List.ofArray

let private shapeOf (candidate: Interval<int>) =
    match candidate with
    | Empty -> "empty"
    | Singleton _ -> "singleton"
    | Union _ -> "union"

let private kinds (candidate: BoundedInterval<int>) =
    candidate.Lower.Kind, candidate.Upper.Kind

let tests =
    testList
        "[Generators]"
        [ reaches "every relation occurs" (pairs |> List.map (fun (a, b) -> BoundedInterval.relate a b)) allRelations
          reaches "every interval shape occurs" (intervals |> List.map shapeOf) [ "empty"; "singleton"; "union" ]
          reaches
              "every boundary shape occurs"
              (pairs |> List.map (fst >> kinds))
              [ Included, Included
                Included, Excluded
                Excluded, Included
                Excluded, Excluded ]
          test "points and touching pieces occur" {
              let isPoint (candidate: BoundedInterval<int>) =
                  candidate.Lower.Value = candidate.Upper.Value

              let hasTouchingPieces =
                  Interval.toList
                  >> List.pairwise
                  >> List.exists (fun (first, second) -> first.Upper.Value = second.Lower.Value)

              Expect.exists (pairs |> List.map fst) isPoint "Points should occur"
              Expect.exists intervals hasTouchingPieces "Pieces separated by one value should occur"
          }
          prop "shrinking a bounded interval only narrows it" (fun (original: BoundedInterval<int>) ->
              shrinkBounded original
              |> Seq.forall (fun smaller ->
                  smaller <> original
                  && Interval.isSubsetOf (Interval.ofBounded smaller) (Interval.ofBounded original)))
          prop "shrinking an interval loses values" (fun (original: Interval<int>) ->
              shrinkInterval original
              |> Seq.forall (fun smaller -> Set.isSubset (denote smaller) (denote original))) ]
