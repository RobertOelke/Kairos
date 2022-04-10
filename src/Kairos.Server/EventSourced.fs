namespace Kairos.Server

type EventSourcedConfig = {
  Query : QueryHandler
  Command : CommandHandler
  EventBus : IEventBus
}

[<RequireQualifiedAccess>]
module EventSourced =

  let create (eventBus : IEventBus) : EventSourcedConfig =
    let query = new QueryHandler()
    let commnad = new CommandHandler()

    {
      Query = query
      Command = commnad
      EventBus = eventBus
    }

  let build (config : EventSourcedConfig) =
    (
      config.Command :> ICommandHandler,
      config.Query :> IQueryHandler
    )

  let addProducer (store : IEventProducer<'event>) (config : EventSourcedConfig) =
    store.OnEvents.Add(config.EventBus.Notify)
    config

  let addConsumer (consumer : IEventConsumer<'event>) (config : EventSourcedConfig) =
    config.EventBus.OnEvent().Add(consumer.Notify)
    config

  let addCommandHandler (handler : CommandHandler<'cmd>) (config : EventSourcedConfig) =
    config.Command.AddHandler(handler)
    config

  let addQueryHandler (handler : QueryHander<'input, 'output>) (config : EventSourcedConfig) =
    let (QueryHander handler) = handler
    config.Query.AddHandler(handler)
    config
