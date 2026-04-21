# interval.fs

[![built with nix](https://builtwithnix.org/badge.svg)](https://builtwithnix.org)
[![Nuget](https://img.shields.io/nuget/v/interval.fs)](https://www.nuget.org/packages/interval.fs)
![License](https://img.shields.io/github/license/schonfinkel/interval.fs)


[![Build](https://github.com/mtrsk/interval.fs/actions/workflows/build.yml/badge.svg)](https://github.com/mtrsk/interval.fs/actions/workflows/build.yml)
![Coverage](https://raw.githubusercontent.com/schonfinkel/interval.fs/gh-pages/coverage.svg)

A small F# implementation of [Allen's Interval Algebra](https://cse.unl.edu/~choueiry/Documents/Allen-CACM1983.pdf) for .NET.

- Generic over any `'T` that implements `IEquatable` and `IComparable`.
- Closed and open boundaries via `Boundary.Kind = Included | Excluded`.
- Validating constructor (`BoundedInterval.TryCreate`) that rejects inverted and zero-width ranges.
- A smart constructor (`Interval.ofSet`) that collapses results to canonical `Empty` / `Singleton` / `Union`.
- Every public operation returns a `Result` or a canonical `Interval`, never throws for business logic.

## Install

```shell
dotnet add package interval.fs
```

## Example

```fsharp
open Interval.Builders
open Interval.Core
open Interval.Functions

// [1, 5) ∩ [3, 7] = [3, 5)
// You can manually construct a record...
let start1 = { Value = 1; Kind = Included }
let end1 = { Value = 5; Kind = Excluded 
// [1, 5)
let a = Singleton { Start = start1; End = end1 } }

// ...or use one of the builders to make it more erngonomic
// [3, 7]
let b = singleton (inc 3) (inc 7)

intersection a b
// Singleton { Start = { Value = 3; Kind = Included }; End = { Value = 5; Kind = Excluded } }

relate a b
// Overlaps
```

For a full walkthrough, types, safe construction with `TryCreate`, `intersection`, `union`, `merge`, the full `Relation` set, and the error model. See [docs/examples.md](./docs/examples.md).

## Libraries

| Namespace / module | Contents |
|---|---|
| `Interval.Core` | `Boundary.Kind`, `Boundary<'T>`, `BoundedInterval<'T>`, `Interval<'T>`, `Relation`, `IntervalError`, the `Interval.ofSet` smart constructor |
| `Interval.Builders` | Quality of life functions like `inc`, `enc`, `BoundedInterval<'T>`, `Interval<'T>`, `Relation`, `IntervalError`, the `Interval.ofSet` smart constructor |
| `Interval.Functions` | `intersection`, `union`, `merge`, `relate`, `invert`, `tryGetSingleton` |
| `Interval.Predicates` | `isEmpty`, , `starts` / `finishes` / `precedes`, and a few internal utilities |

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

The suite combines example-based Expecto tests and FsCheck-driven property tests

### Contributing

Found a bug or want to suggest an improvement? Open an issue or PR and the ping the maintainer.

## Acknowledgements

- [The original paper](https://cse.unl.edu/~choueiry/Documents/Allen-CACM1983.pdf), J.F. Allen, 1983.
- [Thomas A. Alspaugh's page](https://thomasalspaugh.org/pub/fnd/allen.html).
- [Marco Perone's blogpost](https://marcosh.github.io/post/2020/05/04/intervals-and-their-relations.html): Some of the APIs and the properties used by the [Availer](https://github.com/marcosh/availer) Haskell library were ported to F#/.NET.
