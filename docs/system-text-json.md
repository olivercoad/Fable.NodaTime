# System.Text.Json

**Fable users:** System.Text.Json runs on .NET, not in Fable JavaScript. For
shared serialization code, use [Thoth](thoth.md) or [Fable.TypedJson](typed-json.md).
You can also use typed DTOs with System.Text.Json on the server; the
[DTO pattern in the Remoting guide](remoting.md) works even if you don't use
Fable.Remoting.

Use your normal Fable.NodaTime types in records and register the included
converters with your serializer options. No extra package is needed.

```fsharp
open System.Text
open Fable.NodaTime

type Event = { eventName: string; start: Instant; duration: Duration }

let options = Json.JsonSerializerOptions() |> SystemTextJson.configure

let encode (value: Event) =
    Json.JsonSerializer.Serialize(value, options)

let decode json =
    Json.JsonSerializer.Deserialize<Event>(json, options)
```

Configure the options once, before first use, and use them for both reading and
writing. For HTTP endpoints, register the converters on the options your web
framework uses.

Date/time fields become JSON strings. Durations are strings of nanoseconds,
so large values keep their precision:

```json
{
  "eventName": "Lunch",
  "start": "2024-01-01T12:00:00.000000000Z",
  "duration": "3600000000000"
}
```

Invalid date/time fields raise `Json.JsonException` during deserialization.

If you also use FSharp.SystemTextJson for F# options or unions, add its converter
**after** the Fable.NodaTime converters:

```fsharp
let options = Json.JsonSerializerOptions() |> SystemTextJson.configure
options.Converters.Add(Json.Serialization.JsonFSharpConverter())
```

