namespace Interval

module Builders =
    open Interval.Core

    /// An endpoint whose value belongs to the interval.
    let inc value = { Value = value; Kind = Included }

    /// An endpoint whose value does not belong to the interval.
    let exc value = { Value = value; Kind = Excluded }
