namespace Interval.Benchmarks

open System
open BenchmarkDotNet.Attributes
open BenchmarkDotNet.Running

open Interval.Builders
open Interval.Core

/// The input distributions a merge is measured against.
type Shape =
    | Scattered
    | Ordered
    | Clumped

module Input =
    let private closed (first, last) =
        BoundedInterval.TryCreate(inc first, inc last) |> Result.toOption

    let private shuffled (items: 'a array) =
        Random(42).Shuffle items
        items

    let private scattered count =
        let rng = Random 42

        Array.init count (fun _ ->
            let first = rng.Next(0, 4 * count)
            first, first + rng.Next(1, 4))

    let private clumped count =
        Array.init count (fun i ->
            let first = (i / 10) * 100 + i % 10
            first, first + 5)

    let bounds shape count =
        match shape with
        | Scattered -> scattered count
        | Ordered -> scattered count |> Array.sort
        | Clumped -> clumped count |> shuffled
        |> Array.choose closed
        |> List.ofArray

    /// Evenly spaced pieces, offset so that the two sets interleave and touch.
    let evenlySpaced offset width count =
        List.init count (fun i -> 4 * i + offset, 4 * i + offset + width)
        |> List.choose closed

// BenchmarkDotNet discovers public classes with settable properties, so these
// two types stay classes. Everything they measure is pure.

[<MemoryDiagnoser; ShortRunJob>]
type MergeBenchmarks() =
    let mutable input = []

    [<Params(10, 1000, 10000)>]
    member val Count = 0 with get, set

    [<ParamsSource("Shapes")>]
    member val Shape = Scattered with get, set

    member _.Shapes = [ Scattered; Ordered; Clumped ]

    [<GlobalSetup>]
    member this.Setup() =
        input <- Input.bounds this.Shape this.Count

    [<Benchmark>]
    member _.OfSeq() = Interval.ofSeq input

[<MemoryDiagnoser; ShortRunJob>]
type SetOperationBenchmarks() =
    let mutable left = Interval.empty
    let mutable right = Interval.empty

    [<Params(10, 1000)>]
    member val Count = 0 with get, set

    [<GlobalSetup>]
    member this.Setup() =
        left <- Interval.ofSeq (Input.evenlySpaced 0 1 this.Count)
        right <- Interval.ofSeq (Input.evenlySpaced 1 2 this.Count)

    [<Benchmark(Baseline = true)>]
    member _.Intersection() = Interval.intersection left right

    [<Benchmark>]
    member _.Union() = Interval.union left right

module Program =
    [<EntryPoint>]
    let main args =
        BenchmarkSwitcher.FromAssembly(typeof<MergeBenchmarks>.Assembly).Run args
        |> ignore

        0
