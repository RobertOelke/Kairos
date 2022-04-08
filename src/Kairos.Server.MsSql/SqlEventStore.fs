namespace Kairos.Server.MsSql

open System
open Kairos.Server
open System.Data.SqlClient

type private SqlEventStoreMsg<'event> =
| Start
| GetStream of EventSource option * AsyncReplyChannel<EventData<'event> list>
| Append of EventsToAppend<'event> * AsyncReplyChannel<unit>

type SqlEventStore<'event>(connectionString: string, ?tableName : string, ?encoder : EventEncoder<'event>, ?decoder : EventDecoder<'event>) =
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

  let appendEvent (connection : SqlConnection) (event : EventData<'event>) =
    task {
      let insert = $"
        INSERT INTO [dbo].[{tableName}] (
          [Source],
          [VersionNr],
          [RecordedAtUtc],
          [EventName],
          [EventData])
        VALUES (
          @Source,
          @VersionNr,
          @RecordedAtUtc,
          @EventName,
          @EventData
        )"
        
      let name, json = encoder event.Event

      use cmd = connection.CreateCommand()
      cmd.CommandText <- insert
      cmd.Parameters.AddWithValue("Source", box event.Source) |> ignore
      cmd.Parameters.AddWithValue("VersionNr", box event.Version) |> ignore
      cmd.Parameters.AddWithValue("RecordedAtUtc", box event.RecordedAtUtc) |> ignore
      cmd.Parameters.AddWithValue("EventName", box name) |> ignore
      if json = null
      then cmd.Parameters.AddWithValue("EventData", box DBNull.Value) |> ignore
      else cmd.Parameters.AddWithValue("EventData", box json) |> ignore
      
      let! _ = cmd.ExecuteNonQueryAsync()

      return ()
    }

  let getCurrentVersion (connection : SqlConnection) (src : EventSource) =
    task {
      use cmd = connection.CreateCommand()
      cmd.CommandText <- $"SELECT COALESCE(MAX([VersionNr]), 0) FROM [dbo].[{tableName}] WHERE [Source] = @Source"
      cmd.Parameters.AddWithValue("Source", box src) |> ignore
      let! version = cmd.ExecuteScalarAsync()

      return (version :?> int64)
    }

  let update (msg : SqlEventStoreMsg<'event>) =
    match msg with
    | Start ->
      task {
        use connection = new SqlConnection(connectionString)
        do! connection.OpenAsync()

        try
          let createTable = $"
            CREATE TABLE [dbo].[{tableName}] (
	          [Source] [uniqueidentifier] NOT NULL,
	          [VersionNr] [bigint] NOT NULL,
	          [RecordedAtUtc] [datetime2](7) NOT NULL,
              [EventName] [varchar](100) NOT NULL,
	          [EventData] [varchar](max) NULL
            )"
            
          use cmd = connection.CreateCommand()
          cmd.CommandText <- createTable
          let! _ = cmd.ExecuteScalarAsync()

          let createIndex = $"CREATE UNIQUE INDEX {tableName}_SourceVersion ON [dbo].[{tableName}] (Source ASC, VersionNr ASC)"
          cmd.CommandText <- createIndex
          let! _ = cmd.ExecuteNonQueryAsync()
          ()
        with
        | _ ->
          ()
      }
      |> Async.AwaitTask

    | GetStream (src, reply) ->
      task {
        use connection = new SqlConnection(connectionString)
        do! connection.OpenAsync()

        let where = if src.IsSome then "WHERE [Source] = @Source" else ""

        let select = $"
          SELECT [Source],
            [VersionNr],
            [RecordedAtUtc],
            [EventName],
            [EventData]
          FROM [dbo].[{tableName}]
          {where}
          ORDER BY [VersionNr]"

        use cmd = connection.CreateCommand()
        cmd.CommandText <- select
        match src with
        | Some src -> cmd.Parameters.AddWithValue("Source", box src) |> ignore
        | None -> ()

        use reader = cmd.ExecuteReader()

        let events = [
          while reader.Read() do
            let name = reader.GetString(3)
            let json =
              if reader.IsDBNull(4) then
                null
              else
                reader.GetString(4)

            yield {
              Source = reader.GetGuid(0)
              Version = reader.GetInt64(1)
              RecordedAtUtc = reader.GetDateTime(2)
              Event = decoder (name, json)
            }
        ]

        do reply.Reply(events)

        return ()
      }
      |> Async.AwaitTask

    | Append (events, reply) ->
      task {
        use connection = new SqlConnection(connectionString)
        do! connection.OpenAsync()
        let! currentVersion = getCurrentVersion connection events.StreamSource

        if events.ExpectedVersion |> Option.map ((<>) currentVersion) = Some true then
          errorEvent.Trigger (new Exception("Append failed"))
          reply.Reply()

        let now = DateTime.UtcNow

        let newEvents =
          events.Events
          |> List.mapi (fun i e -> {
              Source = events.StreamSource
              Version = currentVersion + 1L + (int64 i)
              RecordedAtUtc = now
              Event = e
            })

        for ev in newEvents do
          do! appendEvent connection ev

        eventsAppended.Trigger(newEvents)
        reply.Reply()
        return ()
      }
      |> Async.AwaitTask

  let agent = StatelessAgent<'event>.Start(update)
  do agent.Post Start
   
  interface IEventStore<'event> with
    member this.Get () : Async<EventData<'event> list> =
      agent.PostAndAsyncReply(fun reply -> GetStream (None, reply))

    member this.GetStream (src : EventSource) : Async<EventData<'event> list> =
      agent.PostAndAsyncReply(fun reply -> GetStream (Some src, reply))

    member this.Append (events : EventsToAppend<'event>) : Async<unit> =
      agent.PostAndAsyncReply(fun reply -> Append (events, reply))

    member this.OnError : IEvent<exn> = errorEvent.Publish

  interface IEventProducer<'event> with
    member this.OnEvents : IEvent<EventData<'event> list> = eventsAppended.Publish