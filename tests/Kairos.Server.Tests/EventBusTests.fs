namespace Kairos.Server.Tests

open Expecto
open Kairos.Server

type IntEvent = Inc

module EventBusTests =
  let allTests =
    testList "Event bus" [
      testAsync "Test" {
        let wrap e = {
          Source = System.Guid.Empty
          Version = 1
          RecordedAtUtc = System.DateTime.Now
          Event = e
        }
        let mutable c = 0

        let bus = EventBus<IntEvent>() :> IEventBus<IntEvent>
        bus.Subscribe(fun events ->
          async {
            do! Async.Sleep(2)
            for e in events do c <- c + 1
            return ()
          })

        Expect.equal c 0 "Is zero"

        do
          [ Inc; Inc; Inc ]
          |> List.map wrap
          |> bus.Notify

        Expect.equal c 0 ""
        do! Async.Sleep(100)
        Expect.equal c 3 ""

        do!
          [ Inc; Inc; Inc ]
          |> List.map wrap
          |> bus.AwaitNotify

        Expect.equal c 6 ""
      }
    ]

