namespace Kairos.Server

open System

// Core

type EventSource = Guid
type VersionNr = int64
type EventName = string
type EventJson = string

type EventsToAppend<'event> = {
  StreamSource : EventSource
  Events : 'event list
  ExpectedVersion : VersionNr option
}

type EventData<'event> = {
  Source : EventSource
  Version : VersionNr
  RecordedAtUtc : DateTime
  Event : 'event
}

type Aggregate<'data> = {
  Source : EventSource
  Version : VersionNr
  RecordedAtUtc : DateTime
  State : 'data
}

type Projection<'state, 'event> = {
  Zero : 'state
  Update : 'state -> 'event -> 'state
}

type EventEncoder<'event> = 'event -> EventName * EventJson
type EventDecoder<'event> = EventName * EventJson -> 'event


// Store

type IEventStore<'event> =
  abstract member Get : unit -> Async<EventData<'event> list>
  abstract member GetStream : EventSource -> Async<EventData<'event> list>
  abstract member Append : EventsToAppend<'event> -> Async<unit>
  abstract member OnError : IEvent<exn>

type IAggregateStore<'state, 'event> =
  inherit IEventStore<'event>
  abstract member GetAggregate : EventSource -> Async<Aggregate<'state> option>


// EventBus

type IEventConsumer<'event> =
  abstract member Notify : EventData<'event> -> unit

type IEventProducer<'event> =
  abstract member OnEvents : IEvent<EventData<'event> list>
  
type IEventBus =
  abstract member OnAll : IObservable<obj>
  abstract member OnEvent<'event> : unit -> IObservable<EventData<'event>>
  abstract member Notify<'event> : EventData<'event> list -> unit


// Command

[<RequireQualifiedAccess>]
type CommandResult<'reason> =
| Ok
| Rejected of 'reason
| Error of exn

type CommandHandler<'cmd, 'reason> = CommandHandler of (EventSource -> 'cmd -> Async<CommandResult<'reason>>)

type ICommandHandler<'rejection> =
  abstract member Handle<'cmd> : EventSource * 'cmd -> Async<CommandResult<'rejection>>


// Query

[<RequireQualifiedAccess>]
type QueryResult<'result> =
| Ok of 'result
| Error of exn

type QueryHandler<'input, 'result> = QueryHandler of ('input -> Async<'result>)

type IQueryHandler =
  abstract member TryHandle<'input, 'result> : 'input -> Async<QueryResult<'result>>

type Async =
  static member CatchCommandResult a =
    async {
      match! a |> Async.Catch with
      | Choice1Of2 x -> return x
      | Choice2Of2 exn -> return CommandResult.Error exn
    }

  static member CatchQueryResult query =
    async {
      match! query |> Async.Catch with
      | Choice1Of2 x -> return x
      | Choice2Of2 exn -> return QueryResult.Error exn
    }