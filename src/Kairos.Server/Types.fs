namespace Kairos.Server

open System

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

[<RequireQualifiedAccess>]
type CommandResult =
| Ok
| Rejected
| NoHandler of Type
| Error of exn

type CommandHandler<'cmd> = EventSource -> 'cmd -> Async<CommandResult>

[<RequireQualifiedAccess>]
type QueryResult<'result> =
| Ok of 'result
| NoHandler
| Error of exn

type EventEncoder<'event> = 'event -> EventName * EventJson
type EventDecoder<'event> = EventName * EventJson -> 'event

type IEventStore<'event> =
  abstract member GetStream : EventSource -> Async<EventData<'event> list>
  abstract member Append : EventsToAppend<'event> -> Async<unit>
  abstract member OnError : IEvent<exn>
  abstract member OnEvents : IEvent<EventData<'event> list>

type IAggregateStore<'state, 'event> =
  inherit IEventStore<'event>
  abstract member GetAggregate : EventSource -> Async<Aggregate<'state> option>

type ICommandHandler =
  abstract member Handle<'cmd> : EventSource * 'cmd -> Async<CommandResult>

type IQueryHandler =
  abstract member TryHandle<'input, 'result> : 'input -> Async<QueryResult<'result>>