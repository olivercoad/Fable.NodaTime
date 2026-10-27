module TypedJsonTests

open Fable.NodaTime
open Fable.NodaTime.TypedJson
open Fable.TypedJson.Schema

module TypedJson = Fable.TypedJson.Json

#if FABLE_COMPILER
open Fable.Core
open Fable.Core.JsInterop

module Runtime = Fable.TypedJson.JS.Json

[<Import("readFileSync", "node:fs")>]
let private readFileSync (path: string, encoding: string) : string = jsNative

[<Import("writeFileSync", "node:fs")>]
let private writeFileSync (path: string, contents: string) : unit = jsNative

let readFile path = readFileSync (path, "utf8")
let writeFile path contents = writeFileSync (path, contents)
#else
module Runtime = Fable.TypedJson.DotNet.Json
let readFile = System.IO.File.ReadAllText

let writeFile (path: string) (contents: string) =
    System.IO.File.WriteAllText(path, contents)
#endif

let equal expected actual =
    if expected <> actual then
        failwithf "Expected %A, got %A" expected actual

let ok =
    function
    | Ok value -> value
    | Error error -> failwithf "Unexpected error: %A" error

let isError =
    function
    | Error _ -> ()
    | Ok value -> failwithf "Expected rejection, got %A" value

type Values =
    {
        instant: Instant
        date: LocalDate
        time: LocalTime
        local: LocalDateTime
        duration: Duration
        zone: DateTimeZone
        zoned: ZonedDateTime
    }

type Nested =
    {
        values: Values list
        optional: Instant option
    }

let values =
    {
        instant = Instant.parse "1969-12-31T23:59:59.123456789Z"
        date = LocalDate.parse "2024-02-29"
        time = LocalTime.parse "12:34:56.123456789"
        local = LocalDateTime.parse "2024-02-29T12:34:56.123456789"
        duration = Duration.parse "-123456789012345678901234567890"
        zone = DateTimeZone.find "America/New_York"
        zoned = ZonedDateTime.parse "2024-11-03T06:30:00.123456789Z[America/New_York]"
    }

let valuesCodec = Runtime.autoStrictWith<Values> NodaTimeCodecs.registry
let nestedCodec = Runtime.autoStrictWith<Nested> NodaTimeCodecs.registry

let checkCodec (codec: IJsonCodec<'T>) value canonical invalid =
    equal (JsonValue.JString canonical) (codec.Encode value)
    equal (Ok value) (codec.Decode(JsonValue.JString canonical))
    equal (JsonSchemaValue.SVStr "string") (Map.find "type" codec.Schema)

    for bad in
        [
            JsonValue.JNull
            JsonValue.JInt 0
            JsonValue.JFloat 1.5
            JsonValue.JBool true
            JsonValue.JArray(box (Array.empty<obj>))
            JsonValue.JMap(box ())
            JsonValue.JString ""
            JsonValue.JString null
            JsonValue.JString invalid
        ] do
        codec.Decode bad |> isError

type Note = Note of string
type Annotated = { note: Note; instant: Instant }

let codecTests () =
    checkCodec NodaTimeCodecs.instant values.instant (Instant.format values.instant) "0000-01-01T00:00:00Z"
    checkCodec NodaTimeCodecs.localDate values.date "2024-02-29" "2023-02-29"
    checkCodec NodaTimeCodecs.localTime values.time "12:34:56.123456789" "24:00:00"
    checkCodec NodaTimeCodecs.localDateTime values.local "2024-02-29T12:34:56.123456789" "2024-01-01T00:00:00Z"
    checkCodec NodaTimeCodecs.duration values.duration (Duration.format values.duration) "01"
    checkCodec NodaTimeCodecs.dateTimeZone values.zone "America/New_York" "Not/AZone"

    checkCodec
        NodaTimeCodecs.zonedDateTime
        values.zoned
        (ZonedDateTime.format values.zoned)
        "2024-11-03T01:30:00-05:00[America/New_York]"

    for text in [ "0"; "9007199254740993"; "-9007199254740993" ] do
        checkCodec NodaTimeCodecs.duration (Duration.parse text) text "1.5"

    for text in
        [
            "2024-11-03T05:30:00.123456789Z[America/New_York]"
            "2024-11-03T06:30:00.123456789Z[America/New_York]"
        ] do
        checkCodec NodaTimeCodecs.zonedDateTime (ZonedDateTime.parse text) text "bad"

    equal
        (Ok(Instant.parse "2024-01-01T00:00:00.100000000Z"))
        (NodaTimeCodecs.instant.Decode(JsonValue.JString "2024-01-01T00:00:00.1Z"))

    equal values (valuesCodec.encode values |> Runtime.decodeText valuesCodec |> ok)

    for optional in [ Some values.instant; None ] do
        let nested =
            {
                values = [ values; values ]
                optional = optional
            }

        equal nested (nestedCodec.encode nested |> Runtime.decodeText nestedCodec |> ok)

    // Date/time codecs reject primitive coercion even with TypedJson's permissive autoWith.
    let permissive = Runtime.autoWith<Values> NodaTimeCodecs.registry
    let json = valuesCodec.encode values

    for text in
        [
            Instant.format values.instant
            LocalDate.format values.date
            LocalTime.format values.time
            LocalDateTime.format values.local
            Duration.format values.duration
            DateTimeZone.id values.zone
            ZonedDateTime.format values.zoned
        ] do
        for token in [ "null"; "0"; "true"; "{}"; "[]"; "\"bad\"" ] do
            let badJson = json.Replace("\"" + text + "\"", token)
            equal false (badJson = json)
            Runtime.decodeText valuesCodec badJson |> isError
            Runtime.decodeText permissive badJson |> isError

    match Runtime.decodeText valuesCodec (json.Replace(Instant.format values.instant, "bad")) with
    | Error(TypedJson.InvalidValue fields) -> equal true (fields |> List.exists (fun field -> field.path = "instant"))
    | other -> failwithf "Expected an error at instant, got %A" other

    match Runtime.decodeText valuesCodec "{" with
    | Error(TypedJson.InvalidText _) -> ()
    | other -> failwithf "Expected malformed JSON error, got %A" other

    let noteCodec =
        Fable.TypedJson.Codec.mk
            (function
            | JsonValue.JString text -> Ok(Note text)
            | _ -> Error "Expected note")
            (fun (Note text) -> JsonValue.JString text)
            (primitiveSchema "string")

    let registry = emptyRegistry |> register noteCodec |> NodaTimeCodecs.register
    let annotatedCodec = Runtime.autoStrictWith<Annotated> registry

    let annotated =
        {
            note = Note "Example"
            instant = values.instant
        }

    equal annotated (annotatedCodec.encode annotated |> Runtime.decodeText annotatedCodec |> ok)

let fixture name =
    "artifacts/typed-json/" + name + ".json"

[<EntryPoint>]
let main args =
    codecTests ()
#if FABLE_COMPILER
    for name in [ "server-typed"; "server-stj" ] do
        equal values (readFile (fixture name) |> Runtime.decodeText valuesCodec |> ok)

    writeFile (fixture "client-typed") (valuesCodec.encode values)
#else
    let options = SystemTextJson.configure (System.Text.Json.JsonSerializerOptions())

    if Array.contains "--verify-client" args then
        let json = readFile (fixture "client-typed")
        equal values (Runtime.decodeText valuesCodec json |> ok)
        equal values (System.Text.Json.JsonSerializer.Deserialize<Values>(json, options))
    else
        System.IO.Directory.CreateDirectory("artifacts/typed-json") |> ignore
        writeFile (fixture "server-typed") (valuesCodec.encode values)
        writeFile (fixture "server-stj") (System.Text.Json.JsonSerializer.Serialize(values, options))
#endif
    printfn "PASS TypedJson: validated codecs, precision, overlaps, nested records, errors, and cross-runtime JSON"
    0
