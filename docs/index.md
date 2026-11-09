# interval.fs documentation

`interval.fs` is an F# implementation of [Allen's Interval Algebra](https://cse.unl.edu/~choueiry/Documents/Allen-CACM1983.pdf) for .NET, built on an exact set algebra of intervals.

## How it fits together

- **Endpoints know their side.** A `Point` is a value and whether it is included. Lower and upper bounds break ties in opposite directions, so points deliberately have no ordering of their own.
- **A `BoundedInterval` is connected and non-empty.** `TryCreate` is the only way to build one, so an invalid interval cannot exist.
- **An `Interval` is canonical.** It is any finite union of bounded intervals, held ascending and pairwise disconnected, so two intervals holding the same values are equal.
- **Set operations are closed and lawful.** `union`, `intersection` and `difference` always return canonical intervals, and form a distributive lattice with the empty interval as its bottom.
- **Relations are decided from endpoints.** `BoundedInterval.relate` returns exactly one of Allen's thirteen relations, and `RelationSet` adds their boolean algebra, converse and composition.

## Start here

- The repository README has the install step and a first example.
- [Examples](examples.md) walks through every operation, the laws they satisfy, and their performance characteristics.

The library targets .NET 10 and has no dependencies beyond `FSharp.Core`.
