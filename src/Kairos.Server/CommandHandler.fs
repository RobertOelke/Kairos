namespace Kairos.Server

open System
open System.Collections.Concurrent

type CommandHandler<'rejection>() =
  let handlers = new ConcurrentDictionary<Type, obj>()

  member this.AddHandler<'cmd, 'rejection> (handler : CommandHandler<'cmd, 'rejection>) =
    handlers.AddOrUpdate(typeof<'cmd>, box handler, fun t o -> (box handler))
    |> ignore
    
  interface ICommandHandler<'rejection> with
    member this.Handle<'cmd>(src : EventSource, cmd : 'cmd) : Async<CommandResult<'rejection>> =
      async {
        match handlers.TryGetValue typeof<'cmd> with
        | true, h ->

          match h with
          | :? CommandHandler<'cmd, 'rejection> as (CommandHandler handler) ->
            return! handler src cmd
          |_ ->
            return CommandResult.NoHandler

        | false, _ -> 
          return CommandResult.NoHandler
      } |> Async.CatchCommandResult