namespace Fable.NodaTime

#if !FABLE_COMPILER
/// Explicit, lossless conversions for the supported ISO range.
[<RequireQualifiedAccess>]
module NodaTimeConversions =
    let toInstant value =
        NodaTime.Text.InstantPattern.ExtendedIso.Parse(Instant.format value).GetValueOrThrow()

    let fromInstant value =
        NodaTime.Text.InstantPattern.ExtendedIso.Format(value) |> Instant.parse

    let toLocalDate value =
        NodaTime.Text.LocalDatePattern.Iso.Parse(LocalDate.format value).GetValueOrThrow()

    let fromLocalDate (value: NodaTime.LocalDate) =
        if value.Calendar <> NodaTime.CalendarSystem.Iso then
            invalidArg "value" "Only ISO is supported"

        NodaTime.Text.LocalDatePattern.Iso.Format(value) |> LocalDate.parse

    let toLocalTime value =
        NodaTime.Text.LocalTimePattern.ExtendedIso.Parse(LocalTime.format value).GetValueOrThrow()

    let fromLocalTime value =
        NodaTime.Text.LocalTimePattern.ExtendedIso.Format(value) |> LocalTime.parse

    let toLocalDateTime value =
        NodaTime.Text.LocalDateTimePattern.ExtendedIso.Parse(LocalDateTime.format value).GetValueOrThrow()

    let fromLocalDateTime (value: NodaTime.LocalDateTime) =
        if value.Calendar <> NodaTime.CalendarSystem.Iso then
            invalidArg "value" "Only ISO is supported"

        NodaTime.Text.LocalDateTimePattern.ExtendedIso.Format(value)
        |> LocalDateTime.parse

    let toDuration value =
        NodaTime.Duration.FromNanoseconds(Duration.toNanoseconds value)

    let fromDuration (value: NodaTime.Duration) =
        value.ToBigIntegerNanoseconds() |> Duration.fromNanoseconds

    let toDateTimeZone value =
        NodaTime.DateTimeZoneProviders.Tzdb.[DateTimeZone.id value]

    let fromDateTimeZone (value: NodaTime.DateTimeZone) =
        if isNull value then
            nullArg "value"

        let zone = NodaTime.DateTimeZoneProviders.Tzdb.GetZoneOrNull value.Id

        if isNull zone || not (value.Equals zone) then
            invalidArg "value" "Expected a zone from NodaTime's TZDB provider"

        DateTimeZone.find value.Id

    let toZonedDateTime value =
        (toInstant (ZonedDateTime.instant value)).InZone(toDateTimeZone (ZonedDateTime.zone value))

    let fromZonedDateTime (value: NodaTime.ZonedDateTime) =
        if value.Calendar <> NodaTime.CalendarSystem.Iso then
            invalidArg "value" "Only ISO is supported"

        ZonedDateTime.ofInstant (fromDateTimeZone value.Zone) (fromInstant (value.ToInstant()))
#endif
