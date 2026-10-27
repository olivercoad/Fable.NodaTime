# Thoth

Install [Fable.NodaTime.Thoth](https://www.nuget.org/packages/Fable.NodaTime.Thoth)
in your Shared project to serialize normal Fable.NodaTime types with Thoth:

```sh
dotnet add Shared/Shared.fsproj package Fable.NodaTime.Thoth
```

The package uses the current Thoth.Json.Core API.

In Shared, pass `NodaTimeCodecs.extra` when generating both the encoder and decoder:

```fsharp
open Fable.NodaTime
open Fable.NodaTime.Thoth
open Thoth.Json.Core
open Thoth.Json.Core.Auto

type Event = { eventName: string; start: Instant; duration: Duration }

let eventEncoder = Encode.Auto.generateEncoder<Event>(extra = NodaTimeCodecs.extra)
let eventDecoder = Decode.Auto.generateDecoder<Event>(extra = NodaTimeCodecs.extra)
```

Generate these coders once and reuse them. They work on both .NET and Fable;
only reading and writing JSON text needs a runtime backend.

Add `Thoth.Json.JavaScript` to your Fable client:

```fsharp
let encode value =
    value |> eventEncoder |> Thoth.Json.JavaScript.Encode.toString 0

let decode json =
    Thoth.Json.JavaScript.Decode.fromString eventDecoder json
```

On the .NET server, use a backend such as `Thoth.Json.System.Text.Json`:

```fsharp
let encode value =
    value |> eventEncoder |> Thoth.Json.System.Text.Json.Encode.toString 0

let decode json =
    Thoth.Json.System.Text.Json.Decode.fromString eventDecoder json
```

Decoding returns `Result<Event, string>`, including an error when a date/time
field is invalid. Date/time values are encoded as strings, preserving nanoseconds
and large durations.

For hand-written coders, compose `NodaTimeCodecs.encodeInstant` /
`NodaTimeCodecs.decodeInstant`, `NodaTimeCodecs.encodeDuration` /
`NodaTimeCodecs.decodeDuration`, and the matching functions for the other supported
date/time types.
