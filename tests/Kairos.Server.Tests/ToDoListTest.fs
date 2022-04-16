namespace Kairos.Server.Tests

open System
open Expecto
open Kairos.Server

module ToDoList = 
  
  type Item = { Nr : int; IsDone : bool; Text : string }

  type ToDoList = { Items : Item list }

  type Command = 
  | AddItem of string
  | MarkAsDone of int
  | Remove of int

  type Rejection =
  | ItemAlreadMarkedAsDone of int
  | ItemNotFound of int

  type Event = 
  | ItemAdded of Item
  | ItemRemoved of int
  | ItemMarkedAsDone of int

  let projection : Projection<ToDoList, Event> =
    {
      Zero = { Items = [] }
      Update =
        fun state ->
          function
          | ItemAdded item -> { state with Items = item :: state.Items }
          | ItemRemoved itemNr -> { state with Items = state.Items |> List.filter (fun i -> i.Nr <> itemNr) }
          | ItemMarkedAsDone itemNr -> { state with Items = state.Items |> List.map (fun i -> if i.Nr <> itemNr then i else { i with IsDone = true }) }
    }

  let behaviour
    (store : IEventStore<Event>)
    (src : EventSource)
    (cmd : Command) =
    async {
      let! events = store.GetStream src
      let state =
        Projection.project projection events
        |> Option.map (fun a -> a.State)
        |> Option.defaultValue projection.Zero

      match cmd with
      | AddItem text ->
        let nextNr =
          match state.Items with
          | [] -> 1
          | lst -> lst |> List.map (fun i -> i.Nr) |> List.max |> ((+) 1)

        return Ok [ ItemAdded { Nr = nextNr; IsDone = false; Text = text } ]
      | MarkAsDone nr ->
        match state.Items |> List.tryFind (fun i -> i.Nr = nr) with
        | Some item ->
          if item.IsDone then
            return Error (ItemAlreadMarkedAsDone nr)
          else
            return Ok [ ItemMarkedAsDone nr ]
        | None -> return Error (ItemNotFound nr)

      | Remove nr ->
        match state.Items |> List.exists (fun i -> i.Nr = nr) with
        | true -> return Ok [ ItemRemoved nr ]
        | false -> return Error (ItemNotFound nr)
    }
    
  let cmdHandler (store : IEventStore<Event>) : CommandHandler<Command, Rejection> = 
    CommandHandler (fun src cmd ->
      async {
        let! res =
          async {
            match! behaviour store src cmd with
            | Ok events ->
              do! store.Append {
                StreamSource = src
                ExpectedVersion = None
                Events = events
              }
              return CommandResult.Ok
            | Error rej ->
              return CommandResult.Rejected rej
          }
          |> Async.Catch

        match res with
        | Choice1Of2 ok -> return ok
        | Choice2Of2 exn -> return CommandResult.Error exn
      }
    )

  let queryById (store : IEventStore<Event>) : QueryHandler<EventSource, ToDoList option> =
    QueryHandler (fun src ->
      async {
        let! events = store.GetStream src
        return
          Projection.project projection events
          |> Option.map (fun a -> a.State)
      }
    )

module ToDoListTest =
  open ToDoList

  let eventSourced () =
    let bus = EventBus()
    let store = InMemoryEventStore<Event>()

    EventSourced.create bus
    |> EventSourced.addProducer store
    |> EventSourced.addCommandHandler (cmdHandler store)
    |> EventSourced.addQueryHandler (queryById store)
    |> EventSourced.build

  let allTests =
    testList "ToDo List" [
      testAsync "Test" {
        let src = Guid.NewGuid()
        let cmd, query = eventSourced()

        let! res = cmd.Handle (src, Command.AddItem "Test")
        Expect.equal res CommandResult.Ok "Added"
        
        let! res = cmd.Handle (src, Command.MarkAsDone 12)
        Expect.equal
          res
          (12 |> ItemNotFound |> CommandResult.Rejected)
          "12 not found"

        let! item = query.TryHandle<EventSource, ToDoList option> src
        Expect.equal
          ({ Items = [ { Nr = 1; IsDone = false; Text = "Test" } ] } |> Some |> QueryResult.Ok)
          item
          "Item loaded by id"
      }
    ]

