namespace Kairos.Server.Tests

open System
open Kairos.Server
open Expecto
open Kairos.Server.Tests

module Program =

  type TestEvent = 
  | EventOne of int
  | EventTwo of bool

  type Counter = { Count : int }

  type CounterEvent = Inc | Dec

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

      testAsync "AggregateStore" {
        let src = Guid.NewGuid()

        let zero = { Count = 0 }

        let update state =
          function
          | Inc -> { state with Count = state.Count + 1 }
          | Dec -> { state with Count = state.Count - 1 }

        let store = InMemoryAggregateStore<Counter, CounterEvent>({ Zero = zero; Update = update }) :> IAggregateStore<Counter, CounterEvent>
        let events = [ Inc; Inc; Dec ]
        
        do! store.Append { StreamSource = src; ExpectedVersion = None; Events = events }

        let! counter = store.GetAggregate src

        Expect.isSome counter ""

        let counter = counter.Value.State

        Expect.equal counter { Count = 1 } ""
      }
  
      EventBusTests.allTests
    ]

  runTestsWithCLIArgs [] [||] tests
  |> ignore