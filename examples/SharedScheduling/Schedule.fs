namespace SharedScheduling

open Fable.NodaTime

/// A daily wall-clock schedule with an explicit zone and DST policy.
type DailySchedule =
    {
        Zone: DateTimeZone
        Time: LocalTime
        Resolution: Resolvers
    }

module Schedule =
    /// The first occurrence strictly after the clock's current instant.
    let next (clock: IClock) schedule =
        let now = clock.GetCurrentInstant()

        let today =
            ZonedDateTime.ofInstant schedule.Zone now
            |> ZonedDateTime.localDateTime
            |> LocalDateTime.date

        let occurrence date =
            LocalDateTime.at date schedule.Time
            |> ZonedDateTime.resolve schedule.Resolution schedule.Zone

        let rec search date =
            let candidate = occurrence date

            if Instant.compare (ZonedDateTime.instant candidate) now > 0 then
                candidate
            else
                search (LocalDate.plusDays 1 date)

        search today
