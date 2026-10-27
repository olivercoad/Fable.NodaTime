namespace Fable.NodaTime.TypedJson

open Fable.NodaTime
open Fable.TypedJson
open Fable.TypedJson.Schema

/// Backend-independent string codecs and registration for all supported date/time types.
[<RequireQualifiedAccess>]
module NodaTimeCodecs =
    let private stringCodec format parse : IJsonCodec<'T> =
        Codec.mk
            (function
            | JsonValue.JString text ->
                parse text
                |> Result.mapError (fun error -> sprintf "Invalid date/time value: %A" error)
            | _ -> Error "Expected a date/time JSON string.")
            (format >> JsonValue.JString)
            (Schema.primitiveSchema "string")

    let instant = stringCodec Instant.format Instant.tryParse
    let localDate = stringCodec LocalDate.format LocalDate.tryParse
    let localTime = stringCodec LocalTime.format LocalTime.tryParse
    let localDateTime = stringCodec LocalDateTime.format LocalDateTime.tryParse
    let duration = stringCodec Duration.format Duration.tryParse
    let dateTimeZone = stringCodec DateTimeZone.id DateTimeZone.tryFind
    let zonedDateTime = stringCodec ZonedDateTime.format ZonedDateTime.tryParse

    /// Add the date/time codecs to an application registry before constructing its model codecs.
    let register (registry: CodecRegistry) =
        registry
        |> Schema.register instant
        |> Schema.register localDate
        |> Schema.register localTime
        |> Schema.register localDateTime
        |> Schema.register duration
        |> Schema.register dateTimeZone
        |> Schema.register zonedDateTime

    /// Use with autoWith or autoStrictWith to serialize domain values as validated strings.
    let registry = register Schema.emptyRegistry
