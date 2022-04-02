namespace Kairos.Server.Remoting

open Kairos.Server
open Fable.Remoting.Json
open Newtonsoft.Json

[<RequireQualifiedAccess>]
module UnionEncoder =
  let private converter = new FableJsonConverter()
  let inline private quoteString s = $"{('"')}{s}{('"')}"
  
  let encoder<'event> () : EventEncoder<'event> =
    fun event ->
      let json = JsonConvert.SerializeObject(event, converter)

      let trimmed =
        if json.StartsWith('{') then
          json.Substring(1, json.Length - 2)
        else
          json

      let deviderPos = trimmed.IndexOf(':')
      let name =
        if deviderPos > 0 then
          trimmed.Substring(1, deviderPos - 2)
        else
          trimmed.Trim('"')

      let event =
        if deviderPos > 0 then
          trimmed.Substring(deviderPos + 1)
        else
          null

      (name, event)

  let decoder<'event> () : EventDecoder<'event> =
    fun (name, json) ->
      let raw =
        if (json = null) then
          quoteString name
        else
          $"{'{'}{quoteString name}{':'}{json}{'}'}"

      if typeof<'event> = typeof<string> then
        unbox<'event> (box raw)
      else
        JsonConvert.DeserializeObject(raw, typeof<'event>, converter) :?> 'event