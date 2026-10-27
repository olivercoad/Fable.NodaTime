namespace Fable.NodaTime

#if FABLE_COMPILER
/// Explicit conversions using the application's Temporal implementation.
[<RequireQualifiedAccess>]
module TemporalConversions =
    let private requireIso calendar =
        if calendar <> "iso8601" then
            invalidArg "value" "Only ISO is supported"

    let toInstant value =
        Backend.temporal.Instant.Parse(Instant.format value)

    let fromInstant (value: Fable.Temporal.Instant) =
        value.ToString(Backend.instantFormat) |> Instant.parse

    /// LocalDate corresponds to Temporal.PlainDate.
    let toLocalDate value =
        Backend.temporal.PlainDate.Parse(LocalDate.format value)

    let fromLocalDate (value: Fable.Temporal.PlainDate) =
        requireIso value.CalendarId
        value.ToString() |> LocalDate.parse

    /// LocalTime corresponds to Temporal.PlainTime.
    let toLocalTime value =
        Backend.temporal.PlainTime.Parse(LocalTime.format value)

    let fromLocalTime (value: Fable.Temporal.PlainTime) =
        value.ToString(Backend.timeFormat) |> LocalTime.parse

    /// LocalDateTime corresponds to Temporal.PlainDateTime.
    let toLocalDateTime value =
        Backend.temporal.PlainDateTime.Parse(LocalDateTime.format value)

    let fromLocalDateTime (value: Fable.Temporal.PlainDateTime) =
        requireIso value.CalendarId
        value.ToString(Backend.localFormat) |> LocalDateTime.parse

    /// Reject calendar components, including days: elapsed time needs no relative date or zone.
    let fromDuration (value: Fable.Temporal.Duration) =
        if value.Years <> 0. || value.Months <> 0. || value.Weeks <> 0. || value.Days <> 0. then
            invalidArg "value" "Only time components are supported"
        // Convert each integer component before scaling; never total in floating point.
        bigint value.Hours * 3600000000000I
        + bigint value.Minutes * 60000000000I
        + bigint value.Seconds * 1000000000I
        + bigint value.Milliseconds * 1000000I
        + bigint value.Microseconds * 1000I
        + bigint value.Nanoseconds
        |> Duration.fromNanoseconds

    /// Emit time-only Temporal.Duration; values outside Temporal's range throw.
    let toDuration value =
        let nanos = Duration.toNanoseconds value
        let magnitude = abs nanos
        let sign = if nanos < 0I then "-" else ""
        let seconds = string (magnitude / 1000000000I)
        let fraction = string (magnitude % 1000000000I) |> fun s -> s.PadLeft(9, '0')

        let result =
            Backend.temporal.Duration.Parse(sign + "PT" + seconds + "." + fraction + "S")

        if fromDuration result <> value then
            invalidArg "value" "Duration cannot be represented exactly by Temporal"

        result

    /// Temporal represents timezones as identifier strings, not standalone objects.
    let toDateTimeZone value = DateTimeZone.id value
    let fromDateTimeZone id = DateTimeZone.find id

    let toZonedDateTime value =
        (toInstant (ZonedDateTime.instant value)).ToZonedDateTimeISO(toDateTimeZone (ZonedDateTime.zone value))

    let fromZonedDateTime (value: Fable.Temporal.ZonedDateTime) =
        requireIso value.CalendarId
        ZonedDateTime.ofInstant (fromDateTimeZone value.TimeZoneId) (fromInstant (value.ToInstant()))
#endif
