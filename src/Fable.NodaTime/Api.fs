namespace Fable.NodaTime

open System.Text.RegularExpressions

module internal Validation =
    let attempt f =
        try
            Ok(f ())
        with _ ->
            Error(InvalidValue "Invalid or out-of-range ISO value")

    let get =
        function
        | Ok value -> value
        | Error error -> invalidArg "value" (string error)

    let require (pattern: string) (text: string) =
        let matched = Regex.Match(text, pattern)

        if not matched.Success || matched.Length <> text.Length then
            invalidArg "text" "Expected portable ISO syntax"

    let range (text: string) =
        if
            text.Length < 10
            || text.[4] <> '-'
            || text.Substring(0, 4) = "0000"
            || text.[0] = '-'
            || text.[0] = '+'
        then
            invalidArg "value" "Supported ISO years are 0001 through 9999"

        text

    let canonical kind text =
        Backend.canonical kind text
        |> fun value -> if kind = IsoKind.Time then value else range value

    let date = "[0-9]{4}-[0-9]{2}-[0-9]{2}"
    let time = "[0-9]{2}:[0-9]{2}:[0-9]{2}(\\.[0-9]{1,9})?"

    let parse kind pattern (text: string) =
        attempt (fun () ->
            require ("^" + pattern + "$") text
            // Temporal accepts leap seconds by constraining them; this API rejects them.
            if kind <> IsoKind.Date then
                let clock = if kind = IsoKind.Time then text else text.Substring(11)

                if clock.Substring(6, 2) = "60" then
                    invalidArg "text" "Leap seconds are unsupported"

            canonical kind text)

[<RequireQualifiedAccess; CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Duration =
    let zero = Duration 0I
    let fromNanoseconds nanos = Duration nanos
    let toNanoseconds (Duration nanos) = nanos
    let fromSeconds (seconds: int64) = Duration(bigint seconds * 1000000000I)
    let fromMinutes (minutes: int64) = Duration(bigint minutes * 60000000000I)
    let fromHours (hours: int64) = Duration(bigint hours * 3600000000000I)
    let fromDays (days: int64) = Duration(bigint days * 86400000000000I)
    let add (Duration a) (Duration b) = Duration(a + b)
    let negate (Duration a) = Duration(-a)
    /// Wire representation: a signed base-10 integer count of nanoseconds.
    let format (Duration nanos) = string nanos

    let tryParse text =
        Validation.attempt (fun () ->
            Validation.require "^-?(0|[1-9][0-9]*)$" text
            Duration(bigint.Parse text))

    let parse text = tryParse text |> Validation.get

[<RequireQualifiedAccess; CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module LocalDate =
    let tryParse text =
        Validation.parse IsoKind.Date Validation.date text |> Result.map LocalDate

    let parse text = tryParse text |> Validation.get
    let format (LocalDate text) = text

    let create year month day =
        sprintf "%04d-%02d-%02d" year month day |> parse

    let private add unit amount (LocalDate text) =
        Backend.addDate unit amount text |> Validation.range |> LocalDate

    /// Add calendar years, clipping an invalid day to the end of the month.
    let plusYears years date = add DateUnit.Years years date
    /// Add calendar months, clipping an invalid day to the end of the month.
    let plusMonths months date = add DateUnit.Months months date
    let plusDays days date = add DateUnit.Days days date

[<RequireQualifiedAccess; CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module LocalTime =
    let tryParse text =
        Validation.parse IsoKind.Time Validation.time text |> Result.map LocalTime

    let parse text = tryParse text |> Validation.get
    let format (LocalTime text) = text

    let create hour minute second nanosecond =
        sprintf "%02d:%02d:%02d.%09d" hour minute second nanosecond |> parse

[<RequireQualifiedAccess; CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module LocalDateTime =
    let tryParse text =
        Validation.parse IsoKind.DateTime (Validation.date + "T" + Validation.time) text
        |> Result.map LocalDateTime

    let parse text = tryParse text |> Validation.get
    let format (LocalDateTime text) = text
    let at (LocalDate date) (LocalTime time) = parse (date + "T" + time)

    let date (LocalDateTime text) =
        text.Substring(0, 10) |> LocalDate.parse

    let time (LocalDateTime text) = text.Substring(11) |> LocalTime.parse
    let private mapDate change value = at (change (date value)) (time value)

    let plusYears years value =
        mapDate (LocalDate.plusYears years) value

    let plusMonths months value =
        mapDate (LocalDate.plusMonths months) value

    let plusDays days value = mapDate (LocalDate.plusDays days) value

[<RequireQualifiedAccess; CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Instant =
    let tryParse text =
        Validation.parse IsoKind.Instant (Validation.date + "T" + Validation.time + "Z") text
        |> Result.map Instant

    let parse text = tryParse text |> Validation.get
    let format (Instant text) = text
    let unixEpoch = parse "1970-01-01T00:00:00Z"

    let plus (Duration nanos) (Instant text) =
        Backend.instantAdd text nanos |> Validation.range |> Instant

    let between (Instant start) (Instant finish) =
        Backend.between start finish |> Duration

    let compare (Instant a) (Instant b) = compare a b

[<RequireQualifiedAccess; CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module DateTimeZone =
    let tryFind id =
        let exists =
            try
                Backend.zoneExists id
            with _ ->
                false

        if exists then
            Ok(DateTimeZone id)
        else
            Error(UnknownTimeZone id)

    let find id = tryFind id |> Validation.get
    let id (DateTimeZone id) = id
    let utc = find "UTC"
    let dataVersion = Backend.tzdbVersion

[<RequireQualifiedAccess; CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module ZonedDateTime =
    let ofInstant zone instant =
        let (Instant text), (DateTimeZone id) = instant, zone
        Backend.local text id |> Validation.range |> ignore
        ZonedDateTime(instant, zone)

    let instant (ZonedDateTime(instant, _)) = instant
    let zone (ZonedDateTime(_, zone)) = zone

    let localDateTime (ZonedDateTime(Instant text, DateTimeZone zone)) =
        Backend.local text zone |> Validation.range |> LocalDateTime

    let offsetSeconds (ZonedDateTime(Instant text, DateTimeZone zone)) = Backend.offset text zone

    let tryResolve resolver (DateTimeZone zone) (LocalDateTime text) =
        try
            Backend.resolve text zone resolver
            |> Validation.range
            |> Instant
            |> ofInstant (DateTimeZone zone)
            |> Ok
        with _ ->
            Error(LocalTimeResolutionFailed(text, zone))

    let resolve resolver zone local =
        tryResolve resolver zone local |> Validation.get

    /// Elapsed arithmetic on the timeline.
    let plus duration value =
        ofInstant (zone value) (Instant.plus duration (instant value))

    /// Calendar arithmetic in local time; the caller must choose a DST policy.
    let private mapLocal resolver change value =
        resolve resolver (zone value) (change (localDateTime value))

    let plusYears resolver years value =
        mapLocal resolver (LocalDateTime.plusYears years) value

    let plusMonths resolver months value =
        mapLocal resolver (LocalDateTime.plusMonths months) value

    let plusDays resolver days value =
        mapLocal resolver (LocalDateTime.plusDays days) value

    /// Portable wire representation: canonical UTC instant followed by [IANA zone ID].
    let format value =
        Instant.format (instant value) + "[" + DateTimeZone.id (zone value) + "]"

    let tryParse (text: string) =
        Validation.attempt (fun () ->
            let i = text.IndexOf('[')

            if i < 0 || not (text.EndsWith "]") then
                invalidArg "text" "Expected instant[zone]"

            ofInstant
                (DateTimeZone.find (text.Substring(i + 1, text.Length - i - 2)))
                (Instant.parse (text.Substring(0, i))))

    let parse text = tryParse text |> Validation.get

module Clocks =
    let system: IClock =
        { new IClock with
            member _.GetCurrentInstant() = Backend.now () |> Instant.parse
        }

    let fixedClock instant : IClock =
        { new IClock with
            member _.GetCurrentInstant() = instant
        }
