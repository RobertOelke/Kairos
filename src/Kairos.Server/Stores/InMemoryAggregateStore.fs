namespace Kairos.Server

open System

type private InMemoryAggregateStoreMsg<'state, 'event> =
| GetStream of EventSource * AsyncReplyChannel<EventData<'event> list>
| GetAggregate of EventSource * AsyncReplyChannel<Aggregate<'state> option>
| Append of EventsToAppend<'event> * AsyncReplyChannel<unit>

type InMemoryAggregateStore<'state, 'event>(zero : 'state, update : 'state -> 'event -> 'state) =
  let errorEvent = Event<exn>()
  let eventsAppended = Event<EventData<'event> list>()

  let update (storage : EventData<'event> list) (msg : InMemoryAggregateStoreMsg<'state, 'event>) =
    match msg with
    | GetStream (src, reply) ->
      async {
        let events = storage |> List.filter (fun e -> e.Source = src)

        do reply.Reply(events)

        return storage
      }

    | GetAggregate (src, reply) ->
      async {
        match storage |> List.filter (fun e -> e.Source = src) with
        | [] -> reply.Reply None
        | events ->
          let update' s (e : EventData<_>) =
            {
              Source = e.Source
              Version = e.Version
              RecordedAtUtc = e.RecordedAtUtc
              State = update s.State e.Event
            }

          events
          |> List.fold update' { Source = Guid.Empty; Version = 0; RecordedAtUtc = DateTime.MinValue; State = zero }
          |> Some
          |> reply.Reply

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
            events.Events
            |> List.mapi (fun i e -> {
                Source = events.StreamSource
                Version = currentVersion + (int64 i) + 1L
                RecordedAtUtc = now
                Event = e
              })
              
          eventsAppended.Trigger(newEvents)
          reply.Reply()
          return storage @ newEvents
        }

  let agent = StatefullAgent<'event, EventData<'event> list>.Start([], update)
   
  interface IAggregateStore<'state, 'event> with
    member this.GetStream (src : EventSource) : Async<EventData<'event> list> =
      agent.PostAndAsyncReply(fun reply -> GetStream (src, reply))

    member this.Append (events : EventsToAppend<'event>) : Async<unit> =
      agent.PostAndAsyncReply(fun reply -> Append (events, reply))

    member this.GetAggregate (src: EventSource) : Async<Aggregate<'state> option> =
      agent.PostAndAsyncReply(fun reply -> GetAggregate (src, reply))

    member this.OnError : IEvent<exn> = errorEvent.Publish
  
  interface IEventProducer<'event> with
    member this.OnEvents : IEvent<EventData<'event> list> = eventsAppended.Publish