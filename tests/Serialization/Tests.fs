module SerializationTests

open Fable.NodaTime
open Fable.NodaTime.Thoth
open SerializationExample
open Thoth.Json.Core
open Thoth.Json.Core.Auto
#if FABLE_COMPILER
open Fable.Core.JsInterop
open Fable.Core
open Fable.SimpleJson
open Thoth.Json.JavaScript

[<Import("readFileSync", "node:fs")>]
let private readFileSync (path: string, encoding: string) : string = jsNative

[<Import("writeFileSync", "node:fs")>]
let private writeFileSync (path: string, contents: string) : unit = jsNative

let readFile path = readFileSync (path, "utf8")
let writeFile path contents = writeFileSync (path, contents)
#else
open Thoth.Json.System.Text.Json
open Newtonsoft.Json
open Fable.Remoting.Json

let readFile = System.IO.File.ReadAllText

let writeFile (path: string) (contents: string) =
    System.IO.File.WriteAllText(path, contents)

let options = SystemTextJson.configure (System.Text.Json.JsonSerializerOptions())

let remotingSettings =
    JsonSerializerSettings(Converters = [| FableJsonConverter() |])
#endif

let equal expected actual =
    if expected <> actual then
        failwithf "Expected %A, got %A" expected actual

let ok =
    function
    | Ok value -> value
    | Error error -> failwithf "Unexpected decoding error: %A" error

let isError =
    function
    | Error _ -> ()
    | Ok value -> failwithf "Expected rejection, got %A" value

type Values =
    {
        Instant: Instant
        Date: LocalDate
        Time: LocalTime
        Local: LocalDateTime
        Duration: Duration
        Zone: DateTimeZone
        Zoned: ZonedDateTime
    }

type Nested =
    {
        Values: Values list
        Optional: Instant option
    }

let values =
    {
        Instant = Instant.parse "1969-12-31T23:59:59.123456789Z"
        Date = LocalDate.parse "2024-02-29"
        Time = LocalTime.parse "12:34:56.123456789"
        Local = LocalDateTime.parse "2024-02-29T12:34:56.123456789"
        Duration = Duration.parse "-123456789012345678901234567890"
        Zone = DateTimeZone.find "America/New_York"
        Zoned = ZonedDateTime.parse "2024-11-03T06:30:00.123456789Z[America/New_York]"
    }

type TransportValues =
    {
        Instant: InstantDTO
        Date: LocalDateDTO
        Time: LocalTimeDTO
        Local: LocalDateTimeDTO
        Duration: DurationDTO
        Zone: DateTimeZoneDTO
        Zoned: ZonedDateTimeDTO
    }

let transportValues: TransportValues =
    {
        Instant = toNodaDTO values.Instant
        Date = toNodaDTO values.Date
        Time = toNodaDTO values.Time
        Local = toNodaDTO values.Local
        Duration = toNodaDTO values.Duration
        Zone = toNodaDTO values.Zone
        Zoned = toNodaDTO values.Zoned
    }

let booking: Booking =
    {
        StartsAt = values.Instant
        Length = values.Duration
        Occurrence = values.Zoned
    }

let dto = BookingDto.ofDomain booking

let inline encodeAuto<'T> (value: 'T) =
    value
    |> Encode.Auto.generateEncoder<'T>(extra = NodaTimeCodecs.extra)
    |> Encode.toString 0

let inline decodeAuto<'T> json =
    Decode.fromString (Decode.Auto.generateDecoder<'T>(extra = NodaTimeCodecs.extra)) json

let inline encodeDTO<'T> (value: 'T) =
    value |> Encode.Auto.generateEncoder<'T>() |> Encode.toString 0

let inline decodeDTO<'T> json =
    Decode.fromString (Decode.Auto.generateDecoder<'T>()) json

let checkCodec (encode: Encoder<'T>) (decode: Decoder<'T>) (value: 'T) canonical invalid =
    let json = encode value |> Encode.toString 0
    equal (Encode.string canonical |> Encode.toString 0) json
    equal value (Decode.fromString decode json |> ok)

    for bad in
        [
            "null"
            "0"
            "true"
            "{}"
            "[]"
            "\"\""
            Encode.string invalid |> Encode.toString 0
        ] do
        Decode.fromString decode bad |> isError

let thothTests () =
    checkCodec
        NodaTimeCodecs.encodeInstant
        NodaTimeCodecs.decodeInstant
        values.Instant
        (Instant.format values.Instant)
        "0000-01-01T00:00:00Z"

    checkCodec NodaTimeCodecs.encodeLocalDate NodaTimeCodecs.decodeLocalDate values.Date "2024-02-29" "2023-02-29"
    checkCodec NodaTimeCodecs.encodeLocalTime NodaTimeCodecs.decodeLocalTime values.Time "12:34:56.123456789" "24:00:00"

    checkCodec
        NodaTimeCodecs.encodeLocalDateTime
        NodaTimeCodecs.decodeLocalDateTime
        values.Local
        "2024-02-29T12:34:56.123456789"
        "2024-01-01T00:00:00Z"

    checkCodec
        NodaTimeCodecs.encodeDuration
        NodaTimeCodecs.decodeDuration
        values.Duration
        (Duration.format values.Duration)
        "01"

    checkCodec
        NodaTimeCodecs.encodeDateTimeZone
        NodaTimeCodecs.decodeDateTimeZone
        values.Zone
        "America/New_York"
        "Not/AZone"

    checkCodec
        NodaTimeCodecs.encodeZonedDateTime
        NodaTimeCodecs.decodeZonedDateTime
        values.Zoned
        (ZonedDateTime.format values.Zoned)
        "2024-11-03T01:30:00-05:00[America/New_York]"

    let nested =
        {
            Values = [ values; values ]
            Optional = Some values.Instant
        }

    let json = encodeAuto nested
    equal nested (decodeAuto<Nested> json |> ok)
    let empty = { Values = []; Optional = None }

    equal empty (encodeAuto empty |> fun json -> decodeAuto<Nested> json |> ok)

    decodeAuto<Values> "{\"Instant\":null}" |> isError

    let validJson = encodeAuto values

    for text in
        [
            Instant.format values.Instant
            LocalDate.format values.Date
            LocalTime.format values.Time
            LocalDateTime.format values.Local
            Duration.format values.Duration
            DateTimeZone.id values.Zone
            ZonedDateTime.format values.Zoned
        ] do
        let badJson = validJson.Replace(Encode.string text |> Encode.toString 0, "null")
        decodeAuto<Values> badJson |> isError

    for text in [ "0"; "9007199254740993"; "-9007199254740993" ] do
        checkCodec NodaTimeCodecs.encodeDuration NodaTimeCodecs.decodeDuration (Duration.parse text) text "1.5"

    // Both occurrences in the autumn overlap must remain distinct.
    for text in
        [
            "2024-11-03T05:30:00.123456789Z[America/New_York]"
            "2024-11-03T06:30:00.123456789Z[America/New_York]"
        ] do
        let value = ZonedDateTime.parse text
        checkCodec NodaTimeCodecs.encodeZonedDateTime NodaTimeCodecs.decodeZonedDateTime value text "bad"

    equal
        (Instant.parse "2024-01-01T00:00:00.100000000Z")
        (Decode.fromString NodaTimeCodecs.decodeInstant "\"2024-01-01T00:00:00.1Z\""
         |> ok)

let dtoTests () =
    // Transport records use ordinary automatic serialization, without NodaTimeCodecs.extra.
    let json = encodeDTO transportValues
    equal transportValues (decodeDTO<TransportValues> json |> ok)
    equal booking (BookingDto.toDomain dto |> ok)

    BookingDto.toDomain
        { dto with
            StartsAt = { InstantDTO.Value = "invalid" }
        }
    |> isError

    BookingDto.toDomain
        { dto with
            Length =
                {
                    DurationDTO.Value = "9007199254740993.0"
                }
        }
    |> isError

    BookingDto.toDomain
        { dto with
            Occurrence =
                {
                    ZonedDateTimeDTO.Value = "2024-11-03T01:30:00-05:00[America/New_York]"
                }
        }
    |> isError

    BookingDto.toDomain
        { dto with
            StartsAt = Unchecked.defaultof<InstantDTO>
        }
    |> isError

    BookingDto.toDomain Unchecked.defaultof<BookingDto> |> isError

#if !FABLE_COMPILER
let jsonException action =
    let mutable rejected = false

    try
        action ()
    with :? System.Text.Json.JsonException ->
        rejected <- true

    if not rejected then
        failwith "Expected JsonException"

let checkSystemTextJson<'T when 'T: equality> value format invalid =
    let json = System.Text.Json.JsonSerializer.Serialize(value, options)
    equal (System.Text.Json.JsonSerializer.Serialize(format value)) json
    equal value (System.Text.Json.JsonSerializer.Deserialize<'T>(json, options))

    for bad in
        [
            "null"
            "0"
            "true"
            "{}"
            "[]"
            "\"\""
            System.Text.Json.JsonSerializer.Serialize(invalid: string)
        ] do
        jsonException (fun () -> System.Text.Json.JsonSerializer.Deserialize<'T>(bad, options) |> ignore)

    jsonException (fun () ->
        System.Text.Json.JsonSerializer.Serialize(Unchecked.defaultof<'T>, options)
        |> ignore)

let systemTextJsonTests () =
    checkSystemTextJson values.Instant Instant.format "0000-01-01T00:00:00Z"
    checkSystemTextJson values.Date LocalDate.format "2023-02-29"
    checkSystemTextJson values.Time LocalTime.format "24:00:00"
    checkSystemTextJson values.Local LocalDateTime.format "2024-01-01T00:00:00Z"
    checkSystemTextJson values.Duration Duration.format "01"
    checkSystemTextJson values.Zone DateTimeZone.id "Not/AZone"
    checkSystemTextJson values.Zoned ZonedDateTime.format "2024-11-03T01:30:00-05:00[America/New_York]"

    equal
        values
        (System.Text.Json.JsonSerializer.Serialize(values, options)
         |> fun json -> System.Text.Json.JsonSerializer.Deserialize<Values>(json, options))
    // A general F# converter must follow the date/time converters so it cannot unwrap their unions.
    let fsharpOptions =
        SystemTextJson.configure (System.Text.Json.JsonSerializerOptions())

    fsharpOptions.Converters.Add(System.Text.Json.Serialization.JsonFSharpConverter())

    for optional in [ Some values.Instant; None ] do
        let nested =
            {
                Values = [ values ]
                Optional = optional
            }

        equal
            nested
            (System.Text.Json.JsonSerializer.Serialize(nested, fsharpOptions)
             |> fun json -> System.Text.Json.JsonSerializer.Deserialize<Nested>(json, fsharpOptions))

    for text in [ "0"; "9007199254740993"; "-9007199254740993" ] do
        checkSystemTextJson (Duration.parse text) Duration.format "1.5"

    equal
        (Instant.parse "2024-01-01T00:00:00.100000000Z")
        (System.Text.Json.JsonSerializer.Deserialize<Instant>("\"2024-01-01T00:00:00.1Z\"", options))

    equal
        values.Instant
        (System.Text.Json.JsonSerializer.Deserialize<Instant>("\"1969-12-31T23:59:59.123456789Z\"", options))

    for text in
        [
            "2024-11-03T05:30:00.123456789Z[America/New_York]"
            "2024-11-03T06:30:00.123456789Z[America/New_York]"
        ] do
        checkSystemTextJson (ZonedDateTime.parse text) ZonedDateTime.format "bad"

let remotingEncode value =
    JsonConvert.SerializeObject(value, remotingSettings)

let remotingDecode json =
    JsonConvert.DeserializeObject<BookingDto>(json, remotingSettings)

let encodeReply (value: Result<BookingDto, string>) =
    JsonConvert.SerializeObject(value, remotingSettings)

let decodeReply json =
    JsonConvert.DeserializeObject<Result<BookingDto, string>>(json, remotingSettings)

let encodeTransport (value: TransportValues) =
    JsonConvert.SerializeObject(value, remotingSettings)

let decodeTransport json =
    JsonConvert.DeserializeObject<TransportValues>(json, remotingSettings)
#else
let remotingEncode (value: BookingDto) = Fable.SimpleJson.Json.serialize value

let remotingDecode json =
    Fable.SimpleJson.Json.parseNativeAs<BookingDto> json

let encodeReply (value: Result<BookingDto, string>) = Fable.SimpleJson.Json.serialize value

let decodeReply json =
    Fable.SimpleJson.Json.parseNativeAs<Result<BookingDto, string>> json

let encodeTransport (value: TransportValues) = Fable.SimpleJson.Json.serialize value

let decodeTransport json =
    Fable.SimpleJson.Json.parseNativeAs<TransportValues> json
#endif

let fixture name =
    "artifacts/serialization/" + name + ".json"

[<EntryPoint>]
let main args =
    thothTests ()
    dtoTests ()
    equal booking (remotingEncode dto |> remotingDecode |> BookingDto.toDomain |> ok)
    equal transportValues (encodeTransport transportValues |> decodeTransport)
#if FABLE_COMPILER
    // Decode real server output, then leave client output for the .NET verification pass.
    for name in [ "server-thoth"; "server-stj" ] do
        equal values (readFile (fixture name) |> fun json -> decodeAuto<Values> json |> ok)

    equal
        booking
        (readFile (fixture "server-remoting")
         |> remotingDecode
         |> BookingDto.toDomain
         |> ok)

    writeFile (fixture "client-thoth") (encodeAuto values)
    writeFile (fixture "client-remoting") (remotingEncode dto)

    for name in [ "server-thoth-dto"; "server-stj-dto" ] do
        equal transportValues (readFile (fixture name) |> decodeDTO<TransportValues> |> ok)

    equal transportValues (readFile (fixture "server-remoting-dto") |> decodeTransport)
    writeFile (fixture "client-thoth-dto") (encodeDTO transportValues)
    writeFile (fixture "client-remoting-dto") (encodeTransport transportValues)

    for name, reply in [ "ok", Ok dto; "error", Error "Invalid booking" ] do
        equal reply (readFile (fixture ("server-reply-" + name)) |> decodeReply)
        writeFile (fixture ("client-reply-" + name)) (encodeReply reply)
#else
    systemTextJsonTests ()

    if Array.contains "--verify-client" args then
        let json = readFile (fixture "client-thoth")
        equal values (decodeAuto<Values> json |> ok)
        equal values (System.Text.Json.JsonSerializer.Deserialize<Values>(json, options))
        let dtoJson = readFile (fixture "client-thoth-dto")
        equal transportValues (decodeDTO<TransportValues> dtoJson |> ok)
        equal transportValues (System.Text.Json.JsonSerializer.Deserialize<TransportValues>(dtoJson))
        equal transportValues (readFile (fixture "client-remoting-dto") |> decodeTransport)

        equal
            booking
            (readFile (fixture "client-remoting")
             |> remotingDecode
             |> BookingDto.toDomain
             |> ok)

        for name, reply in [ "ok", Ok dto; "error", Error "Invalid booking" ] do
            equal reply (readFile (fixture ("client-reply-" + name)) |> decodeReply)
    else
        System.IO.Directory.CreateDirectory("artifacts/serialization") |> ignore
        writeFile (fixture "server-thoth") (encodeAuto values)
        writeFile (fixture "server-stj") (System.Text.Json.JsonSerializer.Serialize(values, options))
        writeFile (fixture "server-remoting") (remotingEncode dto)
        writeFile (fixture "server-thoth-dto") (encodeDTO transportValues)
        let dtoJson = System.Text.Json.JsonSerializer.Serialize(transportValues)
        equal transportValues (System.Text.Json.JsonSerializer.Deserialize<TransportValues>(dtoJson))
        writeFile (fixture "server-stj-dto") dtoJson
        writeFile (fixture "server-remoting-dto") (encodeTransport transportValues)

        for name, reply in [ "ok", Ok dto; "error", Error "Invalid booking" ] do
            writeFile (fixture ("server-reply-" + name)) (encodeReply reply)

        let mutable saved = false

        let api =
            BookingApi.create (fun value ->
                async {
                    saved <- true
                    return value
                })

        equal (Ok dto) (api.Save dto |> Async.RunSynchronously)
        saved <- false

        api.Save
            { dto with
                Length = { DurationDTO.Value = "invalid" }
            }
        |> Async.RunSynchronously
        |> isError

        equal false saved
#endif
    printfn "PASS serialization: validated codecs, nested values, precision, overlap identity, and cross-runtime JSON"
    0
