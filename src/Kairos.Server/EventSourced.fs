namespace Kairos.Server

type EventSourcedConfig<'rejection> = {
  Query : QueryHandler
  Command : CommandHandler<'rejection>
  EventBus : IEventBus
}

[<RequireQualifiedAccess>]
module EventSourced =

  let create (eventBus : IEventBus) : EventSourcedConfig<'rejection> =
    let query = new QueryHandler()
    let commnad = new CommandHandler<'rejection>()

    {
      Query = query
      Command = commnad
      EventBus = eventBus
    }

  let build (config : EventSourcedConfig<'rejection>) =
    (
      config.Command :> ICommandHandler<'rejection>,
      config.Query :> IQueryHandler
    )

  let addProducer (store : IEventProducer<'event>) (config : EventSourcedConfig<'rejection>) =
    store.OnEvents.Add(config.EventBus.Notify)
    config

  let addConsumer (consumer : IEventConsumer<'event>) (config : EventSourcedConfig<'rejection>) =
    config.EventBus.OnEvent().Add(consumer.Notify)
    config

  let addIntegrationConsumer
    (chooser : 'domain -> 'integration option)
    (consumer : IEventConsumer<'integration>)
    (config : EventSourcedConfig<'rejection>) =
    config.EventBus
      .OnEvent<'domain>()
      |> Observable.choose(fun e ->
        e.Event
        |> chooser
        |> Option.map (fun i -> {
            Source = e.Source
            Version = e.Version
            RecordedAtUtc = e.RecordedAtUtc
            Event = i
          })
        )
      |> Observable.subscribe(consumer.Notify)
      |> ignore

    config

  let addCommandHandler (handler : CommandHandler<'cmd, 'rejection>) (config : EventSourcedConfig<'rejection>) =
    config.Command.AddHandler(handler)
    config

  let addQueryHandler (QueryHandler handler) (config : EventSourcedConfig<'rejection>) =
    config.Query.AddHandler(handler)
    config
