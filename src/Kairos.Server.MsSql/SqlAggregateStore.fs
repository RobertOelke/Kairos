namespace Kairos.Server.MsSql

open System
open Kairos.Server

type private SqlAggregateStoreMsg<'state, 'event> =
| GetAggregate of EventSource * AsyncReplyChannel<Aggregate<'state> option>
| Get of AsyncReplyChannel<EventData<'event> list>
| GetStream of EventSource * AsyncReplyChannel<EventData<'event> list>
| Append of EventsToAppend<'event> * AsyncReplyChannel<unit>

type SqlAggregateStore<'state, 'event>(
  connectionString: string,
  projection : Projection<'state, 'event>,
  ?tableName : string, 
  ?encoder : EventEncoder<'event>,
  ?decoder : EventDecoder<'event>) =
  let errorEvent = Event<exn>()
  let eventsAppended = Event<EventData<'event> list>()

  let tableName =
    tableName
    |> Option.defaultValue typeof<'event>.Name

  let encoder =
    encoder
    |> Option.defaultValue (UnionEncoder.encoder<'event>())

  let decoder =
    decoder
    |> Option.defaultValue (UnionEncoder.decoder<'event>())

  let eventStore =
    new SqlEventStore<'event>(connectionString, tableName, encoder, decoder)
    
  do (eventStore :> IEventProducer<'event>).OnEvents.Add(eventsAppended.Trigger)
  do (eventStore :> IEventStore<'event>).OnError.Add(errorEvent.Trigger)

  let update (msg : SqlAggregateStoreMsg<'state, 'event>) =
    match msg with
    | GetStream (src, reply) ->
      async {
        let! stream = (eventStore :> IEventStore<'event>).GetStream src
        reply.Reply stream
      }
      
    | Get reply ->
      async {
        let! stream = (eventStore :> IEventStore<'event>).Get ()
        reply.Reply stream
      }

    | Append (events, reply) ->
      async {
        do! (eventStore :> IEventStore<'event>).Append events
        reply.Reply()
      }

    | GetAggregate (src, reply) ->
      async {
        match! (eventStore :> IEventStore<'event>).GetStream  src with
        | [] -> reply.Reply None
        | events ->
          let update' s (e : EventData<_>) =
            {
              Source = e.Source
              Version = e.Version
              RecordedAtUtc = e.RecordedAtUtc
              State = projection.Update s.State e.Event
            }

          events
          |> List.fold update' { Source = Guid.Empty; Version = 0; RecordedAtUtc = DateTime.MinValue; State = projection.Zero }
          |> Some
          |> reply.Reply

        return ()
      }

  let agent = StatelessAgent<'event>.Start(update)
   
  interface IAggregateStore<'state, 'event> with
    member this.Get () : Async<EventData<'event> list> =
      agent.PostAndAsyncReply Get

    member this.GetStream (src : EventSource) : Async<EventData<'event> list> =
      agent.PostAndAsyncReply(fun reply -> GetStream (src, reply))

    member this.Append (events : EventsToAppend<'event>) : Async<unit> =
      agent.PostAndAsyncReply(fun reply -> Append (events, reply))

    member this.GetAggregate (src: EventSource) : Async<Aggregate<'state> option> =
      agent.PostAndAsyncReply(fun reply -> GetAggregate (src, reply))

    member this.OnError : IEvent<exn> = errorEvent.Publish
  
  interface IEventProducer<'event> with
    member this.OnEvents : IEvent<EventData<'event> list> = eventsAppended.Publish