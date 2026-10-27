# Fable.Remoting

Use Fable.NodaTime's typed DTO variants in your RPC records. Keep the normal
types in your domain model. You can use the same DTO conversion pattern with
other serializers; only the RPC interface below is specific to Remoting.

Define the records in Shared:

```fsharp
open Fable.NodaTime

type Event = { eventName: string; start: Instant; duration: Duration }
type EventDTO = { eventName: string; start: InstantDTO; duration: DurationDTO }
```

Put the records and these conversion functions in Shared:

```fsharp
let toEventDTO (value: Event) : EventDTO =
    { eventName = value.eventName
      start = toNodaDTO value.start
      duration = toNodaDTO value.duration }

let fromEventDTO (dto: EventDTO) : Result<Event, TimeError> =
    result {
        let! start = fromNodaDTO dto.start
        let! duration = fromNodaDTO dto.duration
        return { eventName = dto.eventName; start = start; duration = duration }
    }
```

`toNodaDTO` picks the DTO type from its input. `fromNodaDTO` validates incoming
data and returns the corresponding domain value or a `TimeError`.
The type system helps to prevent accidentally mixing up DTO types in your conversion code.

Use `EventDTO` in your Remoting interface:

```fsharp
type IEventsApi =
    { Save: EventDTO -> Async<Result<EventDTO, string>> }
```

Convert before sending, and validate after receiving, on both client and server.
For example, a client calling this API can return a validated domain value:

```fsharp
let saveEvent (api: IEventsApi) (value: Event) = async {
    let! response = api.Save (toEventDTO value)
    return result {
        let! dto = response
        return! fromEventDTO dto |> Result.mapError (sprintf "%A")
    }
}
```

On the server, call `fromEventDTO` before saving or running domain logic, and
convert the response with `toEventDTO`. The [complete example](../examples/Serialization/Remoting.fs)
shows this pattern with a booking API.

The same helpers support `LocalDateDTO`, `LocalTimeDTO`, `LocalDateTimeDTO`,
`DateTimeZoneDTO`, and `ZonedDateTimeDTO`. Remoting serializes these as ordinary
records, so no custom serializer registration is needed.
