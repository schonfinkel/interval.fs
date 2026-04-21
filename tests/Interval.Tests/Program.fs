module Main

open Expecto

[<Tests>]
let tests =
    testList
        "Tests"
        [ Interval.Tests.TryCreate.unitTests
          Interval.Tests.Intersection.unitTests
          Interval.Tests.Union.unitTests
          Interval.Tests.Relate.unitTests
          Interval.Tests.Properties.propertyTests ]

[<EntryPoint>]
let main argv = runTestsWithCLIArgs [] argv tests
