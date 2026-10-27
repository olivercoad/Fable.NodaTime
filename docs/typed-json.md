# Fable.TypedJson

Install [Fable.NodaTime.TypedJson](https://www.nuget.org/packages/Fable.NodaTime.TypedJson)
in your Shared project to serialize normal Fable.NodaTime types with TypedJson:

```sh
dotnet add Shared/Shared.fsproj package Fable.NodaTime.TypedJson
```

Define your model in Shared:

```fsharp
open Fable.NodaTime
open Fable.NodaTime.TypedJson

type Event = { eventName: string; start: Instant; duration: Duration }
```

Add `Fable.TypedJson.JS` to your Fable client and use the supplied registry:

```fsharp
let eventCodec =
    Fable.TypedJson.JS.Json.autoStrictWith<Event> NodaTimeCodecs.registry

let encode (value: Event) = eventCodec.encode value

let decode json =
    Fable.TypedJson.JS.Json.decodeText eventCodec json
```

On the server, add `Fable.TypedJson.DotNet` and use its adapter instead.
Use matching versions of the TypedJson core and adapter packages.

```fsharp
let eventCodec =
    Fable.TypedJson.DotNet.Json.autoStrictWith<Event> NodaTimeCodecs.registry

let encode (value: Event) = eventCodec.encode value

let decode json =
    Fable.TypedJson.DotNet.Json.decodeText eventCodec json
```

Build the codec once and reuse it. Always pass the registry with `autoWith` or
`autoStrictWith` so date/time fields use these codecs. Encoding preserves
nanoseconds and large durations as strings. Decoding returns a `Result` with
errors pointing to invalid fields.

If you already have a custom registry, add the date/time codecs with
`NodaTimeCodecs.register`. Individual codecs, such as `NodaTimeCodecs.instant`
and `NodaTimeCodecs.duration`, are also available for custom composition.
