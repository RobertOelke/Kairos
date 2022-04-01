namespace Kairos.Server

open System

type private EventBusMsg<'event> =
| Subscribe of EventsHandler<'event>
| Notify of EventData<'event> list
| AwaitNotify of EventData<'event> list * AsyncReplyChannel<unit>

type EventBus<'event>() =

  let update (handlers : EventsHandler<'event> list) (msg : EventBusMsg<'event>) =
    match msg with
    | Subscribe handler ->
      async {
        return handler :: handlers
      }
    | Notify events ->
      async {
        do!
          handlers
          |> List.map (fun h -> h events)
          |> Async.Parallel
          |> Async.Ignore

        return handlers
      }
    | AwaitNotify (events, reply) ->
      async {
        do!
          handlers
          |> List.map (fun h -> h events)
          |> Async.Parallel
          |> Async.Ignore

        do reply.Reply()

        return handlers
      }

  let statefullAgent = StatefullAgent<_,_>.Start([], update)

  interface IEventBus<'event> with
    member this.Subscribe (handler : EventsHandler<'event>) =
      statefullAgent.Post (Subscribe handler)

    member this.Notify (events : EventData<'event> list) : unit =
      statefullAgent.Post (Notify events)

    member this.AwaitNotify (events : EventData<'event> list) : Async<unit> =
      statefullAgent.PostAndAsyncReply(fun r -> AwaitNotify (events, r))