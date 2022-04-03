namespace Kairos.Server

type EventSourcedConfig = {
  Query : QueryHandler
  Command : CommandHandler
  EventBus : IEventBus
}

[<RequireQualifiedAccess>]
module EventSourced =

  let create () : EventSourcedConfig =
    let query = new QueryHandler()
    let commnad = new CommandHandler()
    let eventBus = new EventBus()

    {
      Query = query
      Command = commnad
      EventBus = eventBus :> IEventBus
    }

  let build (config : EventSourcedConfig) =
    (
      config.Command :> ICommandHandler,
      config.Query :> IQueryHandler
    )

  let addProducer (store : IEventProducer<'event>) (config : EventSourcedConfig) =
    store.OnEvents.Add(config.EventBus.Notify)
    config

  let addCommandHandler (handler : CommandHandler<'cmd>) (config : EventSourcedConfig) =
    config.Command.AddHandler(handler)
    config

  let addQueryHandler (createHandler : IEventBus -> 'input -> Async<'output>) (config : EventSourcedConfig) =
    config.Query.AddHandler(createHandler config.EventBus)
    config
