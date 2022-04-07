namespace Kairos.Server

open System
open System.Collections.Concurrent

type QueryHandler() =
  let handlers = new ConcurrentDictionary<Type * Type, obj>()
  
  member this.AddHandler<'input, 'result> (handler : 'input -> Async<'result>) =
    let key = typeof<'input>, typeof<'result>
    handlers.AddOrUpdate(key, box handler, fun k old -> (box handler)) |> ignore

  interface IQueryHandler with
    member this.TryHandle<'input, 'result> (input : 'input) : Async<QueryResult<'result>> =
      async {
        try
          let key = typeof<'input>, typeof<'result>
          match handlers.TryGetValue(key) with
          | true, h ->
            match h with
            | :? ('input -> Async<'result>) as handlerFunction ->
              let! result = handlerFunction input
              return QueryResult.Ok result
            | _ ->
              return QueryResult.Error (new Exception($"No handler for: {typeof<'intput>.Name} -> {typeof<'result>.Name}"))
          | false, _ ->
              return QueryResult.Error (new Exception($"No handler for: {typeof<'intput>.Name} -> {typeof<'result>.Name}"))
        with
        | e -> return QueryResult.Error e
      } |> Async.CatchQueryResult