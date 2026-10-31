/// The algebraic contract of intervals, stated as FsCheck properties.
module Interval.Tests.Laws

open Expecto
open FsCheck
open FsCheck.FSharp

open Interval.Core
open Interval.Tests.Generators
open Interval.Tests.Model

let config =
    { FsCheckConfig.defaultConfig with
        maxTest = 1000
        arbitrary = [ typeof<Arbitraries> ] }

let prop name property =
    testPropertyWithConfig config name property

type private I = Interval<int>

let private union = Interval.union
let private inter = Interval.intersection
let private diff = Interval.difference

let private homomorphism =
    testList
        "model"
        [ prop "union is set union" (fun (a: I) (b: I) -> denote (union a b) = Set.union (denote a) (denote b))
          prop "intersection is set intersection" (fun (a: I) (b: I) ->
              denote (inter a b) = Set.intersect (denote a) (denote b))
          prop "difference is set difference" (fun (a: I) (b: I) ->
              denote (diff a b) = Set.difference (denote a) (denote b))
          prop "contains agrees with the model" (fun (a: I) ->
              let held = denote a
              probes |> List.forall (fun v -> Interval.contains v a = held.Contains v))
          prop "a bounded interval's contains agrees with the model" (fun (b: BoundedInterval<int>) ->
              probes |> List.forall (fun v -> BoundedInterval.contains v b = holds v b)) ]

let private canonicity =
    let gapBetween (held: Set<int>) (first: BoundedInterval<int>, second: BoundedInterval<int>) =
        [ first.Upper.Value .. second.Lower.Value ]
        |> List.exists (fun v -> not (held.Contains v))

    testList
        "canonical form"
        [ prop "pieces are separated by values the interval does not hold" (fun (a: I) ->
              let held = denote a
              piecesOf a |> List.pairwise |> List.forall (gapBetween held))
          prop "pieces ascend" (fun (a: I) ->
              let pieces = piecesOf a
              pieces = List.sort pieces && pieces = List.distinct pieces)
          testPropertyWithConfig config "any covering fragments rebuild the same value" (fun () ->
              Prop.forAll (Arb.fromGen intervalWithFragments) (fun (whole, fragments) -> ofPieces fragments = whole))
          prop "toArray then ofArray is the identity" (fun (a: I) -> Interval.ofArray (Interval.toArray a) = a)
          prop "isConnected means at most one piece" (fun (a: I) ->
              Interval.isConnected a = (List.length (piecesOf a) <= 1)) ]

let private lattice =
    testList
        "distributive lattice with bottom"
        [ prop "∪ commutes" (fun (a: I) (b: I) -> union a b = union b a)
          prop "∩ commutes" (fun (a: I) (b: I) -> inter a b = inter b a)
          prop "∪ associates" (fun (a: I) (b: I) (c: I) -> union (union a b) c = union a (union b c))
          prop "∩ associates" (fun (a: I) (b: I) (c: I) -> inter (inter a b) c = inter a (inter b c))
          prop "∪ is idempotent" (fun (a: I) -> union a a = a)
          prop "∩ is idempotent" (fun (a: I) -> inter a a = a)
          prop "∅ is the ∪ identity" (fun (a: I) -> union a Interval.empty = a && union Interval.empty a = a)
          prop "∅ annihilates ∩" (fun (a: I) -> Interval.isEmpty (inter a Interval.empty))
          prop "absorption over ∪" (fun (a: I) (b: I) -> union a (inter a b) = a)
          prop "absorption over ∩" (fun (a: I) (b: I) -> inter a (union a b) = a)
          prop "∩ distributes over ∪" (fun (a: I) (b: I) (c: I) -> inter a (union b c) = union (inter a b) (inter a c))
          prop "∪ distributes over ∩" (fun (a: I) (b: I) (c: I) -> union a (inter b c) = inter (union a b) (union a c)) ]

let private order =
    testList
        "subset order and difference"
        [ prop "a ⊆ b iff a ∩ b = a" (fun (a: I) (b: I) -> Interval.isSubsetOf a b = (inter a b = a))
          prop "a ⊆ b iff a ∪ b = b" (fun (a: I) (b: I) -> Interval.isSubsetOf a b = (union a b = b))
          prop "a ∩ b ⊆ a" (fun (a: I) (b: I) -> Interval.isSubsetOf (inter a b) a)
          prop "a ⊆ a ∪ b" (fun (a: I) (b: I) -> Interval.isSubsetOf a (union a b))
          prop "a − a = ∅" (fun (a: I) -> Interval.isEmpty (diff a a))
          prop "a − ∅ = a" (fun (a: I) -> diff a Interval.empty = a)
          prop "∅ − a = ∅" (fun (a: I) -> Interval.isEmpty (diff Interval.empty a))
          prop "(a − b) ∩ b = ∅" (fun (a: I) (b: I) -> Interval.isEmpty (inter (diff a b) b))
          prop "(a − b) ∪ (a ∩ b) = a" (fun (a: I) (b: I) -> union (diff a b) (inter a b) = a) ]

let private construction =
    testList
        "constructors, hull and mapping"
        [ prop "unions is a fold of union" (fun (items: I array) ->
              Interval.unions items = Array.fold union Interval.empty items)
          prop "List.sum is unions" (fun (items: I array) -> Array.sum items = Interval.unions items)
          prop "intersections is a fold of intersection" (fun (first: I) (rest: I array) ->
              Interval.intersections first rest = Array.fold inter first rest)
          prop "ofArray is order-independent" (fun (items: BoundedInterval<int> array) ->
              Interval.ofArray items = Interval.ofArray (Array.rev items))
          prop "ofArray is a fold of union" (fun (items: BoundedInterval<int> array) ->
              Interval.ofArray items = Array.fold
                  (fun acc item -> union acc (Interval.ofBounded item))
                  Interval.empty
                  items)
          prop "operators match the named operations" (fun (a: I) (b: I) ->
              a + b = union a b && a * b = inter a b && a - b = diff a b)
          prop "hull is connected and covers its input" (fun (a: I) ->
              let spanned = Interval.hull a
              Interval.isConnected spanned && Interval.isSubsetOf a spanned)
          prop "hull is idempotent" (fun (a: I) -> Interval.hull (Interval.hull a) = Interval.hull a)
          prop "hull is the least connected cover" (fun (a: I) (cover: BoundedInterval<int>) ->
              let candidate = Interval.ofBounded cover

              not (Interval.isSubsetOf a candidate)
              || Interval.isSubsetOf (Interval.hull a) candidate)
          prop "mapMonotonic shifts the model" (fun (a: I) ->
              denote (Interval.mapMonotonic ((+) 2) a) = Set.map ((+) 2) (denote a))
          prop "mapMonotonic id is the identity" (fun (a: I) -> Interval.mapMonotonic id a = a)
          prop "mapMonotonic composes" (fun (a: I) ->
              Interval.mapMonotonic ((+) 1) (Interval.mapMonotonic ((*) 3) a) = Interval.mapMonotonic
                  ((*) 3 >> (+) 1)
                  a) ]

let tests =
    testList "[Laws] intervals" [ homomorphism; canonicity; lattice; order; construction ]
