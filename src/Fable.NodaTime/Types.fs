namespace Fable.NodaTime

/// A point on the global timeline, with nanosecond precision.
type Instant = internal Instant of string
/// An ISO calendar date, without a timezone.
type LocalDate = internal LocalDate of string
/// A wall-clock time, without a date or timezone.
type LocalTime = internal LocalTime of string
/// An ISO date and wall-clock time, without a timezone.
type LocalDateTime = internal LocalDateTime of string
/// Exact elapsed time, measured in nanoseconds.
type Duration = internal Duration of bigint
/// A named IANA timezone. Identifiers are preserved, including aliases.
type DateTimeZone = internal DateTimeZone of string
/// An instant together with the named zone used to view it.
type ZonedDateTime = internal ZonedDateTime of Instant * DateTimeZone

/// NodaTime local mapping: reject ambiguity/gaps, or choose earlier and shift gaps forward.
[<RequireQualifiedAccess>]
type Resolvers =
    | Strict
    | Lenient

type TimeError =
    | InvalidValue of string
    | UnknownTimeZone of string
    | LocalTimeResolutionFailed of local: string * zone: string

/// Clocks are injectable so domain code need not read the system clock.
type IClock =
    abstract GetCurrentInstant: unit -> Instant

[<RequireQualifiedAccess>]
type internal IsoKind =
    | Instant
    | Date
    | Time
    | DateTime

[<RequireQualifiedAccess>]
type internal DateUnit =
    | Years
    | Months
    | Days
