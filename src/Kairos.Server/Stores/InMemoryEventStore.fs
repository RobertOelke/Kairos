namespace Kairos.Server

open System

type private InMemoryEventStoreMsg<'event> =
| GetStream of EventSource * AsyncReplyChannel<EventData<'event> list>
| Append of EventsToAppend<'event> * AsyncReplyChannel<unit>

type InMemoryEventStore<'event>() =
  let errorEvent = Event<exn>()
  let eventsAppended = Event<EventData<'event> list>()

  let update (storage : EventData<'event> list) (msg : InMemoryEventStoreMsg<'event>) =
    match msg with
    | GetStream (src, reply) ->
      async {
        let events = storage |> List.filter (fun e -> e.Source = src)

        do reply.Reply(events)

        return storage
      }
    | Append (events, reply) ->
      async {
        let oldEvents = storage |> List.filter (fun e -> e.Source = events.StreamSource)

        let currentVersion =
          match oldEvents with
          | [] -> 0L
          | lst -> lst |> List.map (fun x -> x.Version) |> List.max
        
        if events.ExpectedVersion |> Option.map ((<>) currentVersion) = Some true then
          errorEvent.Trigger (new Exception("Append failed"))
          reply.Reply()
          return storage

        else
          let now = DateTime.UtcNow
          let newEvents =
            oldEvents
            @ (events.Events
              |> List.mapi (fun i e -> {
                  Source = events.StreamSource
                  Version = currentVersion + (int64 i) + 1L
                  RecordedAtUtc = now
                  Event = e
                })
              )
              
          eventsAppended.Trigger(newEvents)
          reply.Reply()
          return storage @ newEvents
        }

  let agent = StatefullAgent<'event, EventData<'event> list>.Start([], update)
   
  interface IEventStore<'event> with
    member this.GetStream (src : EventSource) : Async<EventData<'event> list> =
      agent.PostAndAsyncReply(fun reply -> GetStream (src, reply))

    member this.Append (events : EventsToAppend<'event>) : Async<unit> =
      agent.PostAndAsyncReply(fun reply -> Append (events, reply))

    member this.OnError : IEvent<exn> = errorEvent.Publish

    member this.OnEvents : IEvent<EventData<'event> list> = eventsAppended.Publish