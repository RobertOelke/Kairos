module Kairos.Server.Remoting.Tests

open Kairos.Server.Remoting

open Expecto

type TestRecord = { Str : string; Boolean : bool; I : int; IO : int option; SL : string list }

type TestEvent =
| NoArgs
| PrimitivArgs of int
| NamedPrimitivArgs of i:int
| Tuple of int * bool
| Record of TestRecord
| AnonymousRecord of {| Name : string; Age : int |}

let testEvents = [
  NoArgs
  PrimitivArgs 1
  PrimitivArgs 0
  PrimitivArgs -10
  NamedPrimitivArgs 42
  Tuple (1, true)
  Tuple (2, false)
  Record { Str = "123"; Boolean = true; I = 21; IO = Some 1; SL = [ "A"; "B"; "C" ] }
  Record { Str = ""; Boolean = false; I = 12; IO = None; SL = [  ] }
  AnonymousRecord {| Name = "Name"; Age = 1 |}
]

let tests =
  testEvents
  |> List.map (fun event ->
    test (sprintf "Event: %A" event) {
      let encoder = UnionEncoder.encoder<TestEvent>()
      let decoder = UnionEncoder.decoder<TestEvent>()

      let eventName, eventData = encoder event

      Expect.isNotEmpty eventName "Eventname"

      let decodedEvent = decoder (eventName, eventData)

      Expect.equal decodedEvent event "Decoded event"
    })
  |> testList "All Tests"

runTestsWithCLIArgs [] [||] tests
|> ignore
