namespace Kairos.Server

open System

module Projection =
  let project (projection : Projection<'state, 'event>) (events : EventData<'event> list) =
    let update (s : Aggregate<_>) (e : EventData<_>) =
      {
        Source = e.Source
        Version = e.Version
        RecordedAtUtc = e.RecordedAtUtc
        State = projection.Update s.State e.Event
      }

    if events = [] then
      None
    else
      let zero = {
        Source = Guid.Empty
        Version = 0
        RecordedAtUtc = DateTime.MinValue
        State = projection.Zero
      }
      events
      |> List.fold update zero
      |> Some