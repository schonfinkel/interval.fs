namespace Interval.Tests

module Generators =
    open FsCheck
    open Interval.Builders
    open Interval.Core
    open Interval.Core.Boundary

    let generateBoundaryKind () =
        gen {
            let! choice = Gen.oneof [ Gen.constant Kind.Excluded; Gen.constant Kind.Included ]
            return choice
        }

    let generateBoundary () =
        gen {
            let! k = generateBoundaryKind ()
            let! n = Arb.generate<int>
            let boundary = { Value = n; Kind = k }
            return boundary
        }

    let rec generateBoundedInterval () =
        gen {
            let! b1 = generateBoundary ()
            let! b2 = generateBoundary ()

            if b1 < b2 then
                return { Start = b1; End = b2 }
            elif b2 < b1 then
                return { Start = b2; End = b1 }
            else
                // Degenerate, triggers a retry
                // TryCreate would reject Start = End.
                return! generateBoundedInterval ()
        }

    /// <summary>
    /// Generates an <see cref="Interval{T}"/> whose <c>Union</c> case is guaranteed to
    /// contain pairwise non-overlapping bounded intervals, matches the invariant the
    /// library's smart constructors are moving toward.
    /// </summary>
    let generateInterval () : Gen<Interval<int>> =
        let boundedGen = generateBoundedInterval ()

        let disjointUnionGen =
            gen {
                let! seeds = Gen.listOfLength 8 (Gen.choose (-100, 100))

                return
                    seeds
                    |> List.distinct
                    |> List.sort
                    |> List.chunkBySize 2
                    |> List.choose (function
                        | [ a; b ] when a < b -> Some { Start = inc a; End = inc b }
                        | _ -> None)
                    |> Set.ofList
                    |> Interval.ofSet
            }

        Gen.frequency [ 1, Gen.constant Empty; 3, Gen.map Singleton boundedGen; 2, disjointUnionGen ]
