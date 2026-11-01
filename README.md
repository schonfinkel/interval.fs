# interval.fs

[![built with nix](https://builtwithnix.org/badge.svg)](https://builtwithnix.org)
[![Nuget](https://img.shields.io/nuget/v/interval.fs)](https://www.nuget.org/packages/interval.fs)
![License](https://img.shields.io/github/license/schonfinkel/interval.fs)


[![Build](https://github.com/schonfinkel/interval.fs/actions/workflows/build.yml/badge.svg)](https://github.com/schonfinkel/interval.fs/actions/workflows/build.yml)
![Coverage](https://schonfinkel.github.io/interval.fs/coverage.svg)

A small F# implementation of [Allen's Interval Algebra](https://cse.unl.edu/~choueiry/Documents/Allen-CACM1983.pdf) for .NET.

- Generic over any `'T` that supports F# comparison.
- Open and closed boundaries, with exact set semantics at touching endpoints.
- `Interval<'T>` values are always canonical, so equal sets of values are equal.
- Union, intersection and difference in linear time; construction sorts in parallel for large inputs.
- All 13 relations, plus relation sets and composition.
- Invalid intervals cannot be constructed. The public API uses arrays throughout.

## Install

```shell
dotnet add package interval.fs
```

## Example

```fsharp
open Interval.Builders
open Interval.Core

// [1, 5)
let a = closedOpen 1 5

// [3, 7]
let b = closed 3 7

Interval.intersection a b // [3, 5)
Interval.union a b // [1, 7]
Interval.difference a b // [1, 3)
Interval.tryRelate a b // Some Overlaps
```

For a full walkthrough of the types, operations, relation algebra and laws, see [docs/examples.md](./docs/examples.md).

## Libraries

| Namespace / module | Contents |
|---|---|
| `Interval.Core` | `Point`, `BoundedInterval`, `Interval` and its `Empty` / `Singleton` / `Union` pattern, `Relation`, `RelationSet`, `IntervalError`, and their operation modules |
| `Interval.Builders` | `inc`, `exc`, `interval`, `closed`, `opened`, `closedOpen`, `openClosed`, `point` |

## Development

### With Nix

The project uses [devenv.sh](https://devenv.sh/), so you don't need a local .NET installation. To start a development shell:

```shell
nix develop --impure
# or
direnv allow .
```

To build the package purely with Nix:

```shell
nix build
```

### Running tests

From either shell:

```shell
dotnet test
```

The suite is mostly FsCheck properties stating the algebra's laws against an independent model, with a few Expecto examples and regressions.

Benchmarks use BenchmarkDotNet:

```shell
dotnet run -c Release --project tests/Interval.Benchmarks -- --filter '*'
```

### Contributing

Found a bug or want to suggest an improvement? Open an issue or PR and the ping the maintainer.

## Acknowledgements

- [The original paper](https://cse.unl.edu/~choueiry/Documents/Allen-CACM1983.pdf), J.F. Allen, 1983.
- [Thomas A. Alspaugh's page](https://thomasalspaugh.org/pub/fnd/allen.html).
- [Marco Perone's blogpost](https://marcosh.github.io/post/2020/05/04/intervals-and-their-relations.html): Some of the APIs and the properties used by the [Availer](https://github.com/marcosh/availer) Haskell library were ported to F#/.NET.
