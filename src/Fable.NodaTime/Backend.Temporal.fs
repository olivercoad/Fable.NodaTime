namespace Fable.NodaTime

#if FABLE_COMPILER
module internal Backend =
    open Fable.Temporal

    // The application supplies native Temporal or installs its chosen polyfill first.
    let temporal = TemporalRuntime.Native

    let instantFormat =
        InstantFormatOptions(fractionalSecondDigits = FractionalSecondDigits.Digits FractionalDigits.Nine)

    let localFormat =
        DateTimeFormatOptions(
            fractionalSecondDigits = FractionalSecondDigits.Digits FractionalDigits.Nine,
            calendarName = CalendarName.Never
        )

    let timeFormat =
        PrecisionOptions(fractionalSecondDigits = FractionalSecondDigits.Digits FractionalDigits.Nine)

    let canonical kind text =
        match kind with
        | IsoKind.Instant -> temporal.Instant.Parse(text).ToString(instantFormat)
        | IsoKind.Date -> temporal.PlainDate.Parse(text).ToString()
        | IsoKind.Time -> temporal.PlainTime.Parse(text).ToString(timeFormat)
        | IsoKind.DateTime -> temporal.PlainDateTime.Parse(text).ToString(localFormat)

    let addDate unit amount text =
        // Field bags take unsigned magnitudes. Widen before negating Int32.MinValue.
        let magnitude = uint32 (abs (int64 amount))

        let fields =
            match unit with
            | DateUnit.Years -> DurationFields(0u, years = magnitude)
            | DateUnit.Months -> DurationFields(0u, months = magnitude)
            | DateUnit.Days -> DurationFields(0u, days = magnitude)

        let date = temporal.PlainDate.Parse text
        let options = OverflowOptions(overflow = Overflow.Constrain)

        let result =
            if amount < 0 then
                date.Subtract(fields, options)
            else
                date.Add(fields, options)

        result.ToString()

    let instantAdd text nanos =
        temporal.Instant
            .FromEpochNanoseconds(temporal.Instant.Parse(text).EpochNanoseconds + nanos)
            .ToString(instantFormat)

    let between a b =
        temporal.Instant.Parse(b).EpochNanoseconds
        - temporal.Instant.Parse(a).EpochNanoseconds

    let zoneExists zone =
        try
            temporal.Instant.Parse("2000-01-01T00:00:00Z").ToZonedDateTimeISO zone |> ignore
            not (zone.StartsWith "+" || zone.StartsWith "-")
        with _ ->
            false

    let resolve text zone resolver =
        // Reproduce NodaTime AtStrictly / AtLeniently with Temporal disambiguation.
        let policy =
            match resolver with
            | Resolvers.Strict -> Disambiguation.Reject
            | Resolvers.Lenient -> Disambiguation.Compatible

        temporal.PlainDateTime
            .Parse(text)
            .ToZonedDateTime(zone, DisambiguationOptions(disambiguation = policy))
            .ToInstant()
            .ToString(instantFormat)

    let local text zone =
        temporal.Instant.Parse(text).ToZonedDateTimeISO(zone).ToPlainDateTime().ToString(localFormat)

    let offset text zone =
        temporal.Instant.Parse(text).ToZonedDateTimeISO(zone).OffsetNanoseconds
        / 1000000000.
        |> int

    let now () =
        temporal.Now.Instant().ToString(instantFormat)

    let tzdbVersion =
        "Application-provided Temporal timezone data (version unavailable)"
#endif
