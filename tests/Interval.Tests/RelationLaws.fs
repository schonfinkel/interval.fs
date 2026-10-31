/// Allen's relations and their algebra, stated as FsCheck properties.
module Interval.Tests.RelationLaws

open Expecto
open FsCheck
open FsCheck.FSharp

open Interval.Builders
open Interval.Core
open Interval.Tests.Generators
open Interval.Tests.Laws
open Interval.Tests.Model

type private B = BoundedInterval<int>

let private relate = BoundedInterval.relate

let private relations =
    testList
        "relate"
        [ prop "exactly the relation the definitions select" (fun (a: B) (b: B) ->
              relationsHolding a b = [ relate a b ] |> Prop.collect (relate a b))
          testPropertyWithConfig config "intervals built for a relation stand in it" (fun () ->
              Prop.forAll (Arb.fromGen relationScenario) (fun (expected, a, b) -> relate a b = expected))
          prop "swapping operands inverts the relation" (fun (a: B) (b: B) -> relate a b = Relation.invert (relate b a))
          prop "every interval equals itself" (fun (a: B) -> relate a a = Equals)
          prop "Equals is value equality" (fun (a: B) (b: B) -> (relate a b = Equals) = (a = b))
          prop "invert is an involution" (fun (r: Relation) -> Relation.invert (Relation.invert r) = r)
          prop "named predicates agree with relate" (fun (a: B) (b: B) ->
              [ Before, BoundedInterval.before
                After, BoundedInterval.after
                Meets, BoundedInterval.meets
                MetBy, BoundedInterval.metBy
                Overlaps, BoundedInterval.overlaps
                OverlappedBy, BoundedInterval.overlappedBy
                Starts, BoundedInterval.starts
                StartedBy, BoundedInterval.startedBy
                Finishes, BoundedInterval.finishes
                FinishedBy, BoundedInterval.finishedBy
                During, BoundedInterval.during
                Contains, BoundedInterval.encloses
                Equals, BoundedInterval.equals ]
              |> List.forall (fun (relation, predicate) -> predicate a b = (relate a b = relation)))
          prop "tryRelate lifts relate to connected intervals" (fun (a: B) (b: B) ->
              Interval.tryRelate (Interval.ofBounded a) (Interval.ofBounded b) = Some(relate a b)) ]

let private setLink =
    let intersect a b =
        denote (Interval.intersection (Interval.ofBounded a) (Interval.ofBounded b))

    testList
        "relations agree with set operations"
        [ prop "a within b means a ⊆ b" (fun (a: B) (b: B) ->
              not (RelationSet.holds RelationSet.within a b)
              || Interval.isSubsetOf (Interval.ofBounded a) (Interval.ofBounded b))
          prop "disjoint intervals share at most one value" (fun (a: B) (b: B) ->
              not (RelationSet.holds RelationSet.disjoint a b) || (intersect a b).Count <= 1)
          prop "Before leaves nothing in common" (fun (a: B) (b: B) ->
              relate a b <> Before || Set.isEmpty (intersect a b))
          prop "concurrent intervals share a value" (fun (a: B) (b: B) ->
              not (RelationSet.holds RelationSet.concur a b)
              || not (Set.isEmpty (intersect a b))) ]

let private booleanAlgebra =
    let (+.) = RelationSet.union
    let ( *. ) = RelationSet.intersect
    let complement = RelationSet.complement

    testList
        "RelationSet boolean algebra"
        [ prop "union commutes" (fun a b -> a +. b = b +. a)
          prop "intersect commutes" (fun a b -> a *. b = b *. a)
          prop "union associates" (fun a b c -> (a +. b) +. c = a +. (b +. c))
          prop "intersect distributes" (fun a b c -> a *. (b +. c) = (a *. b) +. (a *. c))
          prop "complement bounds" (fun a ->
              a +. complement a = RelationSet.all && a *. complement a = RelationSet.empty)
          prop "complement is an involution" (fun a -> complement (complement a) = a)
          prop "De Morgan" (fun a b -> complement (a +. b) = complement a *. complement b)
          prop "ofList and toList round trip" (fun a -> RelationSet.ofList (RelationSet.toList a) = a)
          prop "contains agrees with toList" (fun a (r: Relation) ->
              RelationSet.contains r a = List.contains r (RelationSet.toList a))
          prop "converse is an involution" (fun a -> RelationSet.converse (RelationSet.converse a) = a)
          prop "converse distributes over union" (fun a b ->
              RelationSet.converse (a +. b) = RelationSet.converse a +. RelationSet.converse b) ]

let private composition =
    let compose = RelationSet.compose
    let equalsOnly = RelationSet.singleton Equals

    let observedCells =
        closedTriples
        |> List.map (fun (a, b, c) ->
            (List.head (relationsHolding a b), List.head (relationsHolding b c)), List.head (relationsHolding a c))
        |> List.groupBy fst
        |> List.map (fun (cell, outcomes) -> cell, outcomes |> List.map snd |> RelationSet.ofList)

    let tripleOf interval = Arb.fromGen (Gen.three interval)

    testList
        "composition"
        [ test "every table cell is exactly what intervals can do" {
              Expect.hasLength observedCells 169 "All pairs of relations should be observed"

              for (first, second), expected in observedCells do
                  Expect.equal (RelationSet.composeRelations first second) expected $"%A{first} ∘ %A{second}"
          }
          testPropertyWithConfig config "half-open triples respect composition" (fun () ->
              Prop.forAll (tripleOf halfOpenInterval) (fun (a, b, c) ->
                  RelationSet.contains (relate a c) (RelationSet.composeRelations (relate a b) (relate b c))))
          prop "associates" (fun a b c -> compose (compose a b) c = compose a (compose b c))
          prop "Equals is the identity" (fun a -> compose equalsOnly a = a && compose a equalsOnly = a)
          prop "distributes over union" (fun a b c ->
              compose a (RelationSet.union b c) = RelationSet.union (compose a b) (compose a c))
          prop "converse reverses composition" (fun a b ->
              RelationSet.converse (compose a b) = compose (RelationSet.converse b) (RelationSet.converse a))
          prop "the empty set annihilates" (fun a -> compose RelationSet.empty a = RelationSet.empty)
          test "mixed boundary kinds fall outside the soundness domain" {
              let bounded lower upper = BoundedInterval.TryCreate(lower, upper)

              match bounded (inc 1) (inc 3), bounded (inc 3) (inc 5), bounded (exc 3) (inc 6) with
              | Ok a, Ok b, Ok c ->
                  Expect.equal (relate a c) Meets "[1,3] meets (3,6]"

                  Expect.isFalse
                      (RelationSet.contains (relate a c) (RelationSet.composeRelations (relate a b) (relate b c)))
                      "Composition is documented as sound only for uniform boundary shapes"
              | _ -> failtest "Endpoints are valid"
          } ]

let tests =
    testList "[Laws] relations" [ relations; setLink; booleanAlgebra; composition ]
