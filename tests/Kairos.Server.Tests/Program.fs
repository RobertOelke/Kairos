module Kairos.Server.Tests

open System
open Kairos.Server
open Expecto

type TestEvent = 
| EventOne of int
| EventTwo of bool

let tests =
  testList "All Tests" [
    testAsync "Append and get" {
      let src = Guid.NewGuid()
      let store = InMemoryEventStore<TestEvent>() :> IEventStore<TestEvent>

      let events = [ EventOne 1; EventOne 2 ]

      do! store.Append { StreamSource = src; ExpectedVersion = None; Events = events }

      let! res = store.GetStream src
      
      Expect.equal res.Length 2 "Two events loaded"
      Expect.equal (res |> List.map (fun e -> e.Event)) events "Appended events loaded"
    }
  ]

runTestsWithCLIArgs [] [||] tests
|> ignore