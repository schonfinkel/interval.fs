namespace Interval

module Builders =
    open Interval.Core
    open Interval.Core.Boundary

    let inc v = { Value = v; Kind = Included }
    let exc v = { Value = v; Kind = Excluded }
    let bounded (s: Boundary.Point<'T>) (e: Boundary.Point<'T>) : BoundedInterval<'T> = { Start = s; End = e }
    let singleton (s: Boundary.Point<'T>) (e: Boundary.Point<'T>) : Interval<'T> = Singleton(bounded s e)
