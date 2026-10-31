module Main

open Expecto

[<Tests>]
let tests = testList "Tests" [ Interval.Tests.Examples.tests ]

[<EntryPoint>]
let main argv = runTestsWithCLIArgs [] argv tests
