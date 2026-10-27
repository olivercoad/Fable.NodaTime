namespace Fable.NodaTime

#if !FABLE_COMPILER
open System.Text

/// Opt-in System.Text.Json support using the portable, validated string formats.
[<RequireQualifiedAccess>]
module SystemTextJson =
    type private StringConverter<'T>(format: 'T -> string, parse: string -> Result<'T, TimeError>) =
        inherit Json.Serialization.JsonConverter<'T>()

        // Null must pass through validation, rather than create an invalid opaque value.
        override _.HandleNull = true

        override _.Read(reader: byref<Json.Utf8JsonReader>, _, _) =
            if reader.TokenType <> Json.JsonTokenType.String then
                raise (Json.JsonException("Expected a date/time JSON string."))

            match parse (reader.GetString()) with
            | Ok value -> value
            | Error error -> raise (Json.JsonException(sprintf "Invalid date/time value: %A" error))

        override _.Write(writer, value, _) =
            if obj.ReferenceEquals(value, null) then
                raise (Json.JsonException("A date/time value cannot be null."))

            writer.WriteStringValue(format value)

    /// Register converters before using the options. Returns the same options for composition.
    /// Register before any general F# union converter. Does not configure record/union/option encoding.
    let configure (options: Json.JsonSerializerOptions) =
        options.Converters.Add(StringConverter<Instant>(Instant.format, Instant.tryParse))
        options.Converters.Add(StringConverter<LocalDate>(LocalDate.format, LocalDate.tryParse))
        options.Converters.Add(StringConverter<LocalTime>(LocalTime.format, LocalTime.tryParse))
        options.Converters.Add(StringConverter<LocalDateTime>(LocalDateTime.format, LocalDateTime.tryParse))
        options.Converters.Add(StringConverter<Duration>(Duration.format, Duration.tryParse))
        options.Converters.Add(StringConverter<DateTimeZone>(DateTimeZone.id, DateTimeZone.tryFind))
        options.Converters.Add(StringConverter<ZonedDateTime>(ZonedDateTime.format, ZonedDateTime.tryParse))
        options
#endif
