open Expecto
open Json.Ok

[<EntryPoint>]
let main argv =
    testList "all tests" [ ReadTests.tests; WriteTests.tests ]
    |> runTestsWithCLIArgs [ No_Spinner ] argv
