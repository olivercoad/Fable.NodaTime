module Tests

open Fable.NodaTime

let equal expected actual =
    if expected <> actual then
        failwithf "Expected %A, got %A" expected actual

let isError result =
    match result with
    | Error _ -> ()
    | Ok value -> failwithf "Expected error, got %A" value

let throws action =
    let mutable thrown = false

    try
        action ()
    with _ ->
        thrown <- true

    if not thrown then
        failwith "Expected an exception"

let zone = DateTimeZone.find "America/New_York"

let resolve policy text =
    ZonedDateTime.resolve policy zone (LocalDateTime.parse text)

let utc text = Instant.parse text

let instantText value =
    value |> ZonedDateTime.instant |> Instant.format

let instantIs text value =
    equal (utc text) (ZonedDateTime.instant value)

let sharedTests =
    [
        "typed transport conversions",
        fun () ->
            let instant = utc "2024-01-01T00:00:00.123456789Z"
            let date = LocalDate.parse "2024-02-29"
            let time = LocalTime.parse "12:34:56.123456789"
            let local = LocalDateTime.parse "2024-02-29T12:34:56.123456789"
            let duration = Duration.parse "9007199254740993"
            let zoned = ZonedDateTime.parse "2024-11-03T06:30:00.123456789Z[America/New_York]"
            equal (Ok instant) (instant |> toNodaDTO |> fromNodaDTO)
            equal (Ok date) (date |> toNodaDTO |> fromNodaDTO)
            equal (Ok time) (time |> toNodaDTO |> fromNodaDTO)
            equal (Ok local) (local |> toNodaDTO |> fromNodaDTO)
            equal (Ok duration) (duration |> toNodaDTO |> fromNodaDTO)
            equal (Ok zone) (zone |> toNodaDTO |> fromNodaDTO)
            equal (Ok zoned) (zoned |> toNodaDTO |> fromNodaDTO)
            let instantDTO: InstantDTO = toNodaDTO instant
            let durationDTO: DurationDTO = toNodaDTO duration
            equal "2024-01-01T00:00:00.123456789Z" instantDTO.Value
            equal "9007199254740993" durationDTO.Value

            equal
                (Ok instant)
                (fromNodaDTO (
                    {
                        InstantDTO.Value = "2024-01-01T00:00:00.123456789Z"
                    }
                    : InstantDTO
                ))

            isError (fromNodaDTO ({ InstantDTO.Value = "bad" }: InstantDTO))
            isError (fromNodaDTO ({ LocalDateDTO.Value = "2023-02-29" }: LocalDateDTO))
            isError (fromNodaDTO ({ LocalTimeDTO.Value = "24:00:00" }: LocalTimeDTO))

            isError (
                fromNodaDTO (
                    {
                        LocalDateTimeDTO.Value = "2024-01-01T00:00:00Z"
                    }
                    : LocalDateTimeDTO
                )
            )

            isError (fromNodaDTO ({ DurationDTO.Value = "1.5" }: DurationDTO))
            isError (fromNodaDTO ({ DateTimeZoneDTO.Value = "Not/AZone" }: DateTimeZoneDTO))

            isError (
                fromNodaDTO (
                    {
                        ZonedDateTimeDTO.Value = "2024-11-03T01:30:00-05:00[America/New_York]"
                    }
                    : ZonedDateTimeDTO
                )
            )

            isError (fromNodaDTO ({ InstantDTO.Value = null }: InstantDTO))
            isError (fromNodaDTO Unchecked.defaultof<InstantDTO>)
            isError (fromNodaDTO Unchecked.defaultof<LocalDateDTO>)
            isError (fromNodaDTO Unchecked.defaultof<LocalTimeDTO>)
            isError (fromNodaDTO Unchecked.defaultof<LocalDateTimeDTO>)
            isError (fromNodaDTO Unchecked.defaultof<DurationDTO>)
            isError (fromNodaDTO Unchecked.defaultof<DateTimeZoneDTO>)
            isError (fromNodaDTO Unchecked.defaultof<ZonedDateTimeDTO>)
        "shared scheduling domain",
        fun () ->
            let schedule: SharedScheduling.DailySchedule =
                {
                    Zone = zone
                    Time = LocalTime.parse "02:30:00"
                    Resolution = Resolvers.Lenient
                }

            let clock = Clocks.fixedClock (utc "2024-03-10T05:00:00Z")
            let next = SharedScheduling.Schedule.next clock schedule
            instantIs "2024-03-10T07:30:00Z" next

            let following =
                SharedScheduling.Schedule.next (Clocks.fixedClock (ZonedDateTime.instant next)) schedule

            instantIs "2024-03-11T06:30:00Z" following
        "nanosecond instant",
        fun () ->
            let value = utc "2024-01-01T00:00:00.123456789Z"
            equal "2024-01-01T00:00:00.123456789Z" (Instant.format value)
            equal (utc "2024-01-01T00:00:00.123456790Z") (Instant.plus (Duration.fromNanoseconds 1I) value)
        "canonical equality",
        fun () ->
            equal (utc "2024-01-01T00:00:00Z") (utc "2024-01-01T00:00:00.000000000Z")
            equal (LocalTime.parse "12:00:00.1") (LocalTime.parse "12:00:00.100000000")
        "negative epoch",
        fun () ->
            equal (utc "1969-12-31T23:59:59.999999999Z") (Instant.plus (Duration.fromNanoseconds -1I) Instant.unixEpoch)

            equal
                -1I
                (Instant.between Instant.unixEpoch (utc "1969-12-31T23:59:59.999999999Z")
                 |> Duration.toNanoseconds)
        "large exact duration",
        fun () ->
            let nanos = 9007199254740993I
            let later = Instant.plus (Duration.fromNanoseconds nanos) Instant.unixEpoch
            equal nanos (Instant.between Instant.unixEpoch later |> Duration.toNanoseconds)
        "duration wire roundtrip",
        fun () ->
            for n in [ 0I; -1I; 9007199254740993I; -12345678901234567890I ] do
                let d = Duration.fromNanoseconds n
                equal d (d |> Duration.format |> Duration.parse)

            for invalid in [ "1\n"; " 1"; "+1"; "01"; "1.0"; null ] do
                isError (Duration.tryParse invalid)

            equal (Duration.fromHours 24L) (Duration.fromDays 1L)
            equal Duration.zero (Duration.add (Duration.fromSeconds 1L) (Duration.fromSeconds -1L))
        "month clipping",
        fun () ->
            equal (LocalDate.parse "2024-02-29") (LocalDate.parse "2024-01-31" |> LocalDate.plusMonths 1)
            equal (LocalDate.parse "2023-02-28") (LocalDate.parse "2023-03-31" |> LocalDate.plusMonths -1)
        "explicit clipping order",
        fun () ->
            equal
                (LocalDate.parse "2025-03-28")
                (LocalDate.parse "2024-02-29" |> LocalDate.plusYears 1 |> LocalDate.plusMonths 1)

            equal
                (LocalDate.parse "2025-01-28")
                (LocalDate.parse "2024-02-29" |> LocalDate.plusYears 1 |> LocalDate.plusMonths -1)
        "signed calendar arithmetic",
        fun () ->
            let date = LocalDate.parse "2024-03-31"
            let local = LocalDateTime.parse "2024-03-31T12:34:56.123456789"
            equal (LocalDate.parse "2024-02-21") (date |> LocalDate.plusMonths -1 |> LocalDate.plusDays -8)

            equal
                (LocalDateTime.parse "2024-02-21T12:34:56.123456789")
                (local |> LocalDateTime.plusMonths -1 |> LocalDateTime.plusDays -8)

            for add in [ LocalDate.plusYears; LocalDate.plusMonths; LocalDate.plusDays ] do
                equal date (add 0 date)

                for amount in [ System.Int32.MinValue; System.Int32.MaxValue ] do
                    throws (fun () -> add amount date |> ignore)

            for add in [ LocalDateTime.plusYears; LocalDateTime.plusMonths; LocalDateTime.plusDays ] do
                equal local (add 0 local)
                throws (fun () -> add System.Int32.MinValue local |> ignore)
        "calendar arithmetic retains local time",
        fun () ->
            let local = LocalDateTime.parse "2024-02-29T12:34:56.123456789"

            let next =
                local
                |> LocalDateTime.plusYears 1
                |> LocalDateTime.plusMonths 1
                |> LocalDateTime.plusDays 6

            equal "2025-04-03T12:34:56.123456789" (LocalDateTime.format next)
            equal (LocalTime.parse "12:34:56.123456789") (LocalDateTime.time next)
            equal next (LocalDateTime.at (LocalDateTime.date next) (LocalDateTime.time next))
        "invalid ISO values",
        fun () ->
            for text in
                [
                    "2023-02-29"
                    "2024-13-01"
                    "0000-01-01"
                    "-000001-01-01"
                    "2024-2-01"
                    "2024-02-30"
                ] do
                isError (LocalDate.tryParse text)

            for text in [ "24:00:00"; "12:60:00"; "12:00:60"; "12:00:00.1234567890"; "12:00:00Z" ] do
                isError (LocalTime.tryParse text)

            for text in
                [
                    "2024-01-01T00:00:60Z"
                    "2024-01-01T00:00:00+00:00"
                    "2024-01-01T00:00:00"
                    "2024-01-01T00:00:00Z[u-ca=iso8601]"
                ] do
                isError (Instant.tryParse text)

            isError (LocalDateTime.tryParse "2024-01-01T00:00:60")
        "range boundaries",
        fun () ->
            for text in [ "0001-01-01T00:00:00Z"; "9999-12-31T23:59:59.999999999Z" ] do
                equal (utc text) (utc text |> Instant.format |> Instant.parse)

            throws (fun () ->
                Instant.plus (Duration.fromNanoseconds 1I) (utc "9999-12-31T23:59:59.999999999Z")
                |> ignore)

            throws (fun () -> LocalDate.plusDays -1 (LocalDate.parse "0001-01-01") |> ignore)

            throws (fun () ->
                LocalDate.parse "9999-01-01"
                |> LocalDate.plusYears 1
                |> LocalDate.plusMonths -12
                |> ignore)

            throws (fun () ->
                LocalDateTime.parse "0001-01-01T00:00:00"
                |> LocalDateTime.plusYears -1
                |> LocalDateTime.plusMonths 12
                |> ignore)
        "projection and resolution boundaries",
        fun () ->
            let west = DateTimeZone.find "Etc/GMT+1"
            let east = DateTimeZone.find "Etc/GMT-1"
            throws (fun () -> ZonedDateTime.ofInstant west (utc "0001-01-01T00:00:00Z") |> ignore)
            throws (fun () -> ZonedDateTime.ofInstant east (utc "9999-12-31T23:59:59Z") |> ignore)
            isError (ZonedDateTime.tryResolve Resolvers.Strict east (LocalDateTime.parse "0001-01-01T00:00:00"))
            isError (ZonedDateTime.tryResolve Resolvers.Strict west (LocalDateTime.parse "9999-12-31T23:59:59"))

            throws (fun () ->
                Instant.plus (Duration.fromNanoseconds -1I) (utc "0001-01-01T00:00:00Z")
                |> ignore)
        "strict wire syntax",
        fun () ->
            for text in [ null; ""; "2024-01-01\n"; "2024-01-01T12:00:00"; "2024-01-01[u-ca=iso8601]" ] do
                isError (LocalDate.tryParse text)

            for text in [ null; ""; "12:00:00\n"; "12:00:00+01:00"; "12:00" ] do
                isError (LocalTime.tryParse text)

            for text in [ null; ""; "2024-01-01T12:00:00Z"; "2024-01-01T12:00:00+01:00" ] do
                isError (LocalDateTime.tryParse text)

            for text in [ null; ""; "2024-01-01T00:00:00Z[UTC]junk"; "2024-01-01T00:00:00Z[UTC][UTC]" ] do
                isError (ZonedDateTime.tryParse text)

            throws (fun () -> LocalTime.create 12 0 0 1000000000 |> ignore)
            throws (fun () -> LocalDate.create 10000 1 1 |> ignore)
        "full supported instant span",
        fun () ->
            let first = utc "0001-01-01T00:00:00Z"
            let last = utc "9999-12-31T23:59:59.999999999Z"
            let span = Instant.between first last
            equal last (Instant.plus span first)
            equal first (Instant.plus (Duration.negate span) last)
        "unknown zones",
        fun () ->
            isError (DateTimeZone.tryFind "Not/AZone")
            isError (DateTimeZone.tryFind null)
            isError (DateTimeZone.tryFind "+05:30")
            equal "UTC" (DateTimeZone.id DateTimeZone.utc)
        "overlap policies",
        fun () ->
            let local = "2024-11-03T01:30:00"
            instantIs "2024-11-03T05:30:00Z" (resolve Resolvers.Lenient local)
            isError (ZonedDateTime.tryResolve Resolvers.Strict zone (LocalDateTime.parse local))
        "gap policies",
        fun () ->
            let local = "2024-03-10T02:30:00.123456789"
            instantIs "2024-03-10T07:30:00.123456789Z" (resolve Resolvers.Lenient local)
            isError (ZonedDateTime.tryResolve Resolvers.Strict zone (LocalDateTime.parse local))
        "ordinary local all policies",
        fun () ->
            for policy in [ Resolvers.Strict; Resolvers.Lenient ] do
                instantIs "2024-01-15T17:00:00Z" (resolve policy "2024-01-15T12:00:00")
        "calendar day versus elapsed day",
        fun () ->
            let before = resolve Resolvers.Strict "2024-03-09T12:00:00"
            let calendar = ZonedDateTime.plusDays Resolvers.Strict 1 before
            let elapsed = ZonedDateTime.plus (Duration.fromDays 1L) before
            equal "2024-03-10T12:00:00.000000000" (ZonedDateTime.localDateTime calendar |> LocalDateTime.format)
            equal "2024-03-10T13:00:00.000000000" (ZonedDateTime.localDateTime elapsed |> LocalDateTime.format)

            equal
                (Duration.fromHours 23L)
                (Instant.between (ZonedDateTime.instant before) (ZonedDateTime.instant calendar))
        "autumn day length",
        fun () ->
            let before = resolve Resolvers.Strict "2024-11-02T12:00:00"
            let after = ZonedDateTime.plusDays Resolvers.Strict 1 before

            equal
                (Duration.fromHours 25L)
                (Instant.between (ZonedDateTime.instant before) (ZonedDateTime.instant after))
        "half hour DST gap",
        fun () ->
            let lordHowe = DateTimeZone.find "Australia/Lord_Howe"
            let local = LocalDateTime.parse "2024-10-06T02:15:00"
            let mapped = ZonedDateTime.resolve Resolvers.Lenient lordHowe local
            instantIs "2024-10-05T15:45:00Z" mapped
            equal "2024-10-06T02:45:00.000000000" (ZonedDateTime.localDateTime mapped |> LocalDateTime.format)
            isError (ZonedDateTime.tryResolve Resolvers.Strict lordHowe local)
        "skipped whole day",
        fun () ->
            let apia = DateTimeZone.find "Pacific/Apia"
            let local = LocalDateTime.parse "2011-12-30T12:00:00"
            let mapped = ZonedDateTime.resolve Resolvers.Lenient apia local
            instantIs "2011-12-30T22:00:00Z" mapped
            equal "2011-12-31T12:00:00.000000000" (ZonedDateTime.localDateTime mapped |> LocalDateTime.format)
            isError (ZonedDateTime.tryResolve Resolvers.Strict apia local)
        "offset and zone projection",
        fun () ->
            let value = ZonedDateTime.ofInstant zone (utc "2024-07-01T00:00:00Z")
            equal -14400 (ZonedDateTime.offsetSeconds value)
            equal "2024-06-30T20:00:00.000000000" (ZonedDateTime.localDateTime value |> LocalDateTime.format)

            let kathmandu =
                ZonedDateTime.ofInstant (DateTimeZone.find "Asia/Kathmandu") (utc "2024-01-01T00:00:00Z")

            equal 20700 (ZonedDateTime.offsetSeconds kathmandu)
        "zoned wire roundtrip",
        fun () ->
            for text in [ "2024-11-03T05:30:00.123456789Z"; "2024-11-03T06:30:00.123456789Z" ] do
                let value = ZonedDateTime.ofInstant zone (utc text)
                equal value (value |> ZonedDateTime.format |> ZonedDateTime.parse)

            isError (ZonedDateTime.tryParse "2024-01-01T00:00:00Z")
            isError (ZonedDateTime.tryParse "2024-01-01T00:00:00Z[Not/AZone]")
        "injected clock",
        fun () ->
            let value = utc "2024-01-01T00:00:00Z"
            equal value ((Clocks.fixedClock value).GetCurrentInstant())
            Clocks.system.GetCurrentInstant() |> Instant.format |> Instant.parse |> ignore
        "calendar fixture sweep",
        fun () ->
            for year in [ 1900; 2000; 2023; 2024; 2100 ] do
                for month in 1..12 do
                    let value = LocalDate.create year month 28
                    equal value (value |> LocalDate.plusMonths 1 |> LocalDate.plusMonths -1)
                    equal value (value |> LocalDate.format |> LocalDate.parse)
        "zone fixture sweep",
        fun () ->
            for id in
                [
                    "UTC"
                    "Europe/London"
                    "Australia/Melbourne"
                    "America/New_York"
                    "Asia/Kathmandu"
                ] do
                for month in [ 1; 4; 7; 10 ] do
                    let zone = DateTimeZone.find id
                    let instant = utc (sprintf "2024-%02d-15T12:34:56.123456789Z" month)
                    let zoned = ZonedDateTime.ofInstant zone instant

                    equal
                        instant
                        (ZonedDateTime.resolve Resolvers.Strict zone (ZonedDateTime.localDateTime zoned)
                         |> ZonedDateTime.instant)
    ]

#if !FABLE_COMPILER
let conversionTests () =
    let jsonOptions =
        SystemTextJson.configure (System.Text.Json.JsonSerializerOptions())

    let duration = Duration.parse "9007199254740993"
    let json = System.Text.Json.JsonSerializer.Serialize(duration, jsonOptions)
    equal "\"9007199254740993\"" json
    equal duration (System.Text.Json.JsonSerializer.Deserialize<Duration>(json, jsonOptions))

    let instant = utc "2024-02-29T12:34:56.123456789Z"
    equal instant (instant |> NodaTimeConversions.toInstant |> NodaTimeConversions.fromInstant)
    let date = LocalDate.parse "2024-02-29"
    equal date (date |> NodaTimeConversions.toLocalDate |> NodaTimeConversions.fromLocalDate)
    let time = LocalTime.parse "12:34:56.123456789"
    equal time (time |> NodaTimeConversions.toLocalTime |> NodaTimeConversions.fromLocalTime)
    let local = LocalDateTime.at date time

    equal
        local
        (local
         |> NodaTimeConversions.toLocalDateTime
         |> NodaTimeConversions.fromLocalDateTime)

    let duration = Duration.fromNanoseconds -9007199254740993I
    equal duration (duration |> NodaTimeConversions.toDuration |> NodaTimeConversions.fromDuration)
    let zoned = ZonedDateTime.ofInstant zone instant

    equal
        zoned
        (zoned
         |> NodaTimeConversions.toZonedDateTime
         |> NodaTimeConversions.fromZonedDateTime)

    throws (fun () ->
        NodaTimeConversions.fromLocalDate (NodaTime.LocalDate(2024, 1, 1, NodaTime.CalendarSystem.Julian))
        |> ignore)

    for id in [ "UTC"; "America/New_York"; "Etc/GMT+1" ] do
        let native = NodaTime.DateTimeZoneProviders.Tzdb.[id]

        equal
            native
            (native
             |> NodaTimeConversions.fromDateTimeZone
             |> NodaTimeConversions.toDateTimeZone)
    // A custom zone can reuse an existing ID while having entirely different rules.
    let offset = NodaTime.Offset.FromHours 1

    let impostor =
        { new NodaTime.DateTimeZone("UTC", true, offset, offset) with
            member _.GetZoneInterval _ =
                NodaTime.TimeZones.ZoneInterval(
                    "Fake UTC",
                    System.Nullable(),
                    System.Nullable(),
                    offset,
                    NodaTime.Offset.Zero
                )
        }

    throws (fun () -> NodaTimeConversions.fromDateTimeZone impostor |> ignore)

    throws (fun () ->
        NodaTimeConversions.fromZonedDateTime ((NodaTimeConversions.toInstant instant).InZone impostor)
        |> ignore)

    throws (fun () -> NodaTimeConversions.fromDateTimeZone null |> ignore)
    throws (fun () -> NodaTimeConversions.fromInstant NodaTime.Instant.MinValue |> ignore)
    throws (fun () -> NodaTimeConversions.toDuration (Duration.fromNanoseconds (10I ** 30)) |> ignore)
    // NodaTime is the oracle for ordinary times, overlaps and gaps of different lengths.
    for id, text in
        [
            "America/New_York", "2024-01-15T12:00:00"
            "America/New_York", "2024-11-03T01:30:00.123456789"
            "America/New_York", "2024-03-10T02:30:00.123456789"
            "Australia/Lord_Howe", "2024-10-06T02:15:00"
            "Pacific/Apia", "2011-12-30T12:00:00"
        ] do
        let zone = DateTimeZone.find id
        let local = LocalDateTime.parse text
        let nativeZone = NodaTimeConversions.toDateTimeZone zone
        let nativeLocal = NodaTimeConversions.toLocalDateTime local

        equal
            (nativeZone.AtLeniently(nativeLocal).ToInstant())
            (ZonedDateTime.resolve Resolvers.Lenient zone local
             |> ZonedDateTime.instant
             |> NodaTimeConversions.toInstant)

        if nativeZone.MapLocal(nativeLocal).Count = 1 then
            equal
                (nativeZone.AtStrictly(nativeLocal).ToInstant())
                (ZonedDateTime.resolve Resolvers.Strict zone local
                 |> ZonedDateTime.instant
                 |> NodaTimeConversions.toInstant)
        else
            throws (fun () -> nativeZone.AtStrictly nativeLocal |> ignore)
            isError (ZonedDateTime.tryResolve Resolvers.Strict zone local)
#endif

#if FABLE_COMPILER
let temporalConversionTests () =
    let temporal = Fable.Temporal.TemporalRuntime.Native

    for text in
        [
            "1969-12-31T23:59:59.999999999Z"
            "0001-01-01T00:00:00Z"
            "9999-12-31T23:59:59.999999999Z"
        ] do
        let value = utc text
        equal value (value |> TemporalConversions.toInstant |> TemporalConversions.fromInstant)

    let date = LocalDate.parse "2024-02-29"
    equal date (date |> TemporalConversions.toLocalDate |> TemporalConversions.fromLocalDate)
    let time = LocalTime.parse "12:34:56.123456789"
    equal time (time |> TemporalConversions.toLocalTime |> TemporalConversions.fromLocalTime)
    let local = LocalDateTime.at date time

    equal
        local
        (local
         |> TemporalConversions.toLocalDateTime
         |> TemporalConversions.fromLocalDateTime)

    for nanos in
        [
            0I
            1I
            -1I
            9007199254740993I
            -12345678901234567890I
            9007199254740991999999999I
        ] do
        let duration = Duration.fromNanoseconds nanos
        equal duration (duration |> TemporalConversions.toDuration |> TemporalConversions.fromDuration)

    equal
        (Duration.fromNanoseconds -3723004005006I)
        (temporal.Duration.Parse("-PT1H2M3.004005006S")
         |> TemporalConversions.fromDuration)

    for text in [ "P1Y"; "P1M"; "P1W"; "P1D"; "-P1DT2H" ] do
        throws (fun () -> temporal.Duration.Parse(text) |> TemporalConversions.fromDuration |> ignore)

    throws (fun () -> Duration.fromNanoseconds (10I ** 30) |> TemporalConversions.toDuration |> ignore)

    equal
        zone
        (zone
         |> TemporalConversions.toDateTimeZone
         |> TemporalConversions.fromDateTimeZone)

    for text in [ "2024-11-03T05:30:00.123456789Z"; "2024-11-03T06:30:00.123456789Z" ] do
        let value = ZonedDateTime.ofInstant zone (utc text)

        equal
            value
            (value
             |> TemporalConversions.toZonedDateTime
             |> TemporalConversions.fromZonedDateTime)

    throws (fun () ->
        temporal.PlainDate.Parse("2024-02-29").WithCalendar("gregory")
        |> TemporalConversions.fromLocalDate
        |> ignore)

    throws (fun () ->
        temporal.PlainDateTime.Parse("2024-02-29T12:00:00").WithCalendar("gregory")
        |> TemporalConversions.fromLocalDateTime
        |> ignore)

    throws (fun () ->
        temporal.Instant.Parse("2024-01-01T00:00:00Z").ToZonedDateTimeISO("UTC").WithCalendar("gregory")
        |> TemporalConversions.fromZonedDateTime
        |> ignore)

    throws (fun () ->
        temporal.Instant.Parse("0000-01-01T00:00:00Z")
        |> TemporalConversions.fromInstant
        |> ignore)

    throws (fun () ->
        temporal.PlainDate.Parse("+010000-01-01")
        |> TemporalConversions.fromLocalDate
        |> ignore)

    throws (fun () -> TemporalConversions.fromDateTimeZone "+01:00" |> ignore)

    throws (fun () ->
        temporal.Instant.Parse("2024-01-01T00:00:00Z").ToZonedDateTimeISO("+01:00")
        |> TemporalConversions.fromZonedDateTime
        |> ignore)

    throws (fun () ->
        temporal.Instant.Parse("0001-01-01T00:00:00Z").ToZonedDateTimeISO("Etc/GMT+1")
        |> TemporalConversions.fromZonedDateTime
        |> ignore)
#endif

[<EntryPoint>]
let main _ =
    let mutable failures = 0

    for name, test in sharedTests do
        try
            test ()
            printfn "PASS shared: %s" name
        with error ->
            failures <- failures + 1
            eprintfn "FAIL %s: %s" name error.Message
#if !FABLE_COMPILER
    try
        conversionTests ()
        printfn "PASS .NET conversions"
    with error ->
        failures <- failures + 1
        eprintfn "FAIL conversions: %s" error.Message
#endif
#if FABLE_COMPILER
    try
        temporalConversionTests ()
        printfn "PASS JavaScript conversions"
    with error ->
        failures <- failures + 1
        eprintfn "FAIL Temporal conversions: %s" error.Message
#endif
    if failures = 0 then
        printfn "Passed %d shared test groups" sharedTests.Length

    if failures <> 0 then
        failwithf "%d test groups failed" failures

    0
