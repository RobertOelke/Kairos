namespace Kairos.Server

type StatefullAgent<'msg, 'state> (initial : 'state, handleMsg : 'state -> 'msg -> Async<'state>) =
  let errorEvent = Event<exn>()

  let inbox = 
    new MailboxProcessor<_>(fun inbox ->
      let rec loop (current : 'state) =
        async {
          let! res = 
            async {
              let! msg = inbox.Receive()
              return! handleMsg current msg
            } |> Async.Catch

          match res with
          | Choice1Of2 newState ->
            return! loop newState

          | Choice2Of2 exn ->
            errorEvent.Trigger exn
            return! loop current
        }

      loop initial
    )

  member this.OnError = errorEvent.Publish

  member this.Start () = inbox.Start ()

  member this.Receive () = inbox.Receive ()

  member this.Post (value : 'msg) = inbox.Post value

  member this.PostAndReply (f : AsyncReplyChannel<'a> -> 'msg) = inbox.PostAndReply f

  member this.PostAndAsyncReply (f : AsyncReplyChannel<'a> -> 'msg) = inbox.PostAndAsyncReply f

  interface System.IDisposable with
    member this.Dispose() =
      (inbox :> System.IDisposable).Dispose()

  static member Start(initialState, f) =
    let agent = new StatefullAgent<_, _>(initialState, f)
    agent.Start()
    agent

type StatelessAgent<'msg> (handleMsg : 'msg -> Async<unit>) =
  let errorEvent = Event<exn>()

  let inbox = 
    new MailboxProcessor<_>(fun inbox ->
      let rec loop () =
        async {
          let! res = 
            async {
              let! msg = inbox.Receive()
              do! (handleMsg msg)
            } |> Async.Catch

          match res with
          | Choice1Of2 () ->
            ()
          | Choice2Of2 exn ->
            errorEvent.Trigger exn

          return! loop ()
        }

      loop ()
    )

  member this.OnError = errorEvent.Publish

  member this.Start () = inbox.Start ()

  member this.Receive () = inbox.Receive ()

  member this.Post (value : 'msg) = inbox.Post value

  member this.PostAndReply (f : AsyncReplyChannel<'a> -> 'msg) = inbox.PostAndReply f

  member this.PostAndAsyncReply (f : AsyncReplyChannel<'a> -> 'msg) = inbox.PostAndAsyncReply f

  interface System.IDisposable with
    member this.Dispose() =
      (inbox :> System.IDisposable).Dispose()

  static member Start f =
    let agent = new StatelessAgent<_>(f)
    agent.Start()
    agent