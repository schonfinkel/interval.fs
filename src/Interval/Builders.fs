namespace Interval

module Builders =
    open Interval.Core

    /// An endpoint whose value belongs to the interval.
    let inc value = { Value = value; Kind = Included }

    /// An endpoint whose value does not belong to the interval.
    let exc value = { Value = value; Kind = Excluded }

    /// The interval between two endpoints, or the empty interval when they enclose nothing.
    let interval (lower: Point<'T>) (upper: Point<'T>) =
        match BoundedInterval.TryCreate(lower, upper) with
        | Ok bounded -> Interval.ofBounded bounded
        | Error _ -> Interval.empty

    /// [lower, upper]
    let closed lower upper = interval (inc lower) (inc upper)

    /// (lower, upper)
    let opened lower upper = interval (exc lower) (exc upper)

    /// [lower, upper)
    let closedOpen lower upper = interval (inc lower) (exc upper)

    /// (lower, upper]
    let openClosed lower upper = interval (exc lower) (inc upper)

    /// [value, value]
    let point value = closed value value
