namespace Fable.NodaTime

#if !FABLE_COMPILER
module internal Backend =
    open NodaTime
    open NodaTime.Text

    let instantPattern = InstantPattern.ExtendedIso

    let instantFormat =
        InstantPattern.CreateWithInvariantCulture("uuuu-MM-dd'T'HH:mm:ss.fffffffff'Z'")

    let datePattern = LocalDatePattern.Iso
    let timePattern = LocalTimePattern.ExtendedIso
    let timeFormat = LocalTimePattern.CreateWithInvariantCulture("HH:mm:ss.fffffffff")
    let localPattern = LocalDateTimePattern.ExtendedIso

    let localFormat =
        LocalDateTimePattern.CreateWithInvariantCulture("uuuu-MM-dd'T'HH:mm:ss.fffffffff")

    let canonical kind text =
        match kind with
        | IsoKind.Instant -> instantPattern.Parse(text).GetValueOrThrow() |> instantFormat.Format
        | IsoKind.Date -> datePattern.Parse(text).GetValueOrThrow() |> datePattern.Format
        | IsoKind.Time -> timePattern.Parse(text).GetValueOrThrow() |> timeFormat.Format
        | IsoKind.DateTime -> localPattern.Parse(text).GetValueOrThrow() |> localFormat.Format

    let addDate unit amount text =
        let date = datePattern.Parse(text).GetValueOrThrow()

        match unit with
        | DateUnit.Years -> date.PlusYears amount
        | DateUnit.Months -> date.PlusMonths amount
        | DateUnit.Days -> date.PlusDays amount
        |> datePattern.Format

    let instantAdd text (nanos: bigint) =
        instantPattern.Parse(text).GetValueOrThrow() + Duration.FromNanoseconds(nanos)
        |> instantFormat.Format

    let between a b =
        (instantPattern.Parse(b).GetValueOrThrow()
         - instantPattern.Parse(a).GetValueOrThrow())
            .ToBigIntegerNanoseconds()

    let zoneExists zone =
        DateTimeZoneProviders.Tzdb.GetZoneOrNull(zone) <> null

    let resolve text zone resolver =
        let value = localPattern.Parse(text).GetValueOrThrow()
        let tz = DateTimeZoneProviders.Tzdb.[zone]

        let zoned =
            match resolver with
            | Fable.NodaTime.Resolvers.Strict -> tz.AtStrictly value
            | Fable.NodaTime.Resolvers.Lenient -> tz.AtLeniently value

        zoned.ToInstant() |> instantFormat.Format

    let local text zone =
        instantPattern.Parse(text).GetValueOrThrow().InZone(DateTimeZoneProviders.Tzdb.[zone]).LocalDateTime
        |> localFormat.Format

    let offset text zone =
        instantPattern.Parse(text).GetValueOrThrow().InZone(DateTimeZoneProviders.Tzdb.[zone]).Offset.Seconds

    let now () =
        SystemClock.Instance.GetCurrentInstant() |> instantFormat.Format

    let tzdbVersion = DateTimeZoneProviders.Tzdb.VersionId
#endif
