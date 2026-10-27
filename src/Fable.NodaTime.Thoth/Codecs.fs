namespace Fable.NodaTime.Thoth

open Fable.NodaTime
open global.Thoth.Json.Core
open global.Thoth.Json.Core.Auto

/// Backend-independent, validated string codecs for Fable.NodaTime using Thoth.Json.Core.
[<RequireQualifiedAccess>]
module NodaTimeCodecs =
    let private validated parse =
        Decode.string
        |> Decode.andThen (fun text ->
            match parse text with
            | Ok value -> Decode.succeed value
            | Error error -> Decode.fail (sprintf "Invalid date/time value: %A" error))

    let encodeInstant = Instant.format >> Encode.string
    let decodeInstant = validated Instant.tryParse
    let encodeLocalDate = LocalDate.format >> Encode.string
    let decodeLocalDate = validated LocalDate.tryParse
    let encodeLocalTime = LocalTime.format >> Encode.string
    let decodeLocalTime = validated LocalTime.tryParse
    let encodeLocalDateTime = LocalDateTime.format >> Encode.string
    let decodeLocalDateTime = validated LocalDateTime.tryParse
    let encodeDuration = Duration.format >> Encode.string
    let decodeDuration = validated Duration.tryParse
    let encodeDateTimeZone = DateTimeZone.id >> Encode.string
    let decodeDateTimeZone = validated DateTimeZone.tryFind
    let encodeZonedDateTime = ZonedDateTime.format >> Encode.string
    let decodeZonedDateTime = validated ZonedDateTime.tryParse

    /// Pass to both Auto encoders and Auto decoders; never decode opaque unions by reflection.
    let extra =
        Extra.empty
        |> Extra.withCustom encodeInstant decodeInstant
        |> Extra.withCustom encodeLocalDate decodeLocalDate
        |> Extra.withCustom encodeLocalTime decodeLocalTime
        |> Extra.withCustom encodeLocalDateTime decodeLocalDateTime
        |> Extra.withCustom encodeDuration decodeDuration
        |> Extra.withCustom encodeDateTimeZone decodeDateTimeZone
        |> Extra.withCustom encodeZonedDateTime decodeZonedDateTime
