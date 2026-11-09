module Main

open Expecto

[<Tests>]
let tests =
    testList
        "Tests"
        [ Interval.Tests.Examples.tests
          Interval.Tests.Laws.tests
          Interval.Tests.RelationLaws.tests
          Interval.Tests.Regressions.tests
          Interval.Tests.GeneratorChecks.tests ]

[<EntryPoint>]
let main argv = runTestsWithCLIArgs [] argv tests
