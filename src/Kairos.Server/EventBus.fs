namespace Kairos.Server

open System

type EventBus() =
  let onAll = Event<obj>()

  interface IEventBus with
    member this.OnAll =
      onAll.Publish

    member this.OnEvent<'event> () : IObservable<EventData<'event>> =
      onAll.Publish
      |> Observable.choose (
          function
          | :? EventData<'event> as event -> Some event
          | _ -> None
        )

    member this.Notify<'event> (events : EventData<'event> list) : unit =
      events
      |> List.iter (box >> onAll.Trigger)