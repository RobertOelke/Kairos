namespace Kairos.Server

open System
open System.Collections.Concurrent

type CommandHandler() =
  let handlers = new ConcurrentDictionary<Type, obj>()

  member this.AddHandler<'cmd> (handler : CommandHandler<'cmd>) =
    handlers.AddOrUpdate(typeof<'cmd>, box handler, fun t o -> (box handler))
    |> ignore
    
  interface ICommandHandler with
    member this.Handle<'cmd>(src : EventSource, cmd : 'cmd) : Async<CommandResult> =
      async {
        try
          let t = typeof<'cmd>

          match handlers.TryGetValue t with
          | true, h ->

            match h with
            | :? CommandHandler<'cmd> as handler ->
              return! handler src cmd
            |_ ->
            return CommandResult.NoHandler t

          | false, _ -> 
            return CommandResult.NoHandler t

        with
        | e -> return CommandResult.Error e
      }