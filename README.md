# Fable.NodaTime

A small F# API for a supported subset of NodaTime semantics, backed by **NodaTime on .NET** and
**Fable.Temporal on JavaScript**. Application code uses
one API without conditional compilation. This is an ISO-only subset,
not a source-compatible replacement for NodaTime. Its main use case is date/time
domain code in a SAFE Stack application with shared DateTime logic.

**Use Fable.NodaTime when you specifically need isomorphic F# date/time code
running on both .NET and JavaScript.** If your code runs only on JavaScript,
prefer [Fable.Temporal](https://github.com/olivercoad/Fable.Temporal) directly;
you do not need this library's cross-runtime abstraction or restricted API.

## SAFE Stack: use in Shared

Put shared date/time types, validation, and scheduling rules in `Shared`, where
both the .NET server and Fable client can use them. This follows SAFE Stack's
[shared-code structure](https://safe-stack.github.io/docs/recipes/client-server/share-code/).
Reference Fable.NodaTime from `Shared.fsproj`; `Client` and `Server` reference
`Shared`.

For example, a domain model and rule in `Shared/Booking.fs` can use the same API
on both sides:

```fsharp
namespace Shared

open Fable.NodaTime

type Booking = { StartsAt: Instant; Length: Duration }

module Booking =
    let finishesAt booking = booking.StartsAt |> Instant.plus booking.Length

    let isUpcoming (clock: IClock) booking =
        Instant.compare booking.StartsAt (clock.GetCurrentInstant()) > 0
```

The server executes these rules through NodaTime; the client executes them through
Temporal. Use `NodaTimeConversions` in server code to access the underlying NodaTime values, and
and `TemporalConversions` in client code to access the underlying Temporal values.

The client must provide Temporal before its compiled application loads, as described below. Inject `IClock` for rules
that depend on the current time; use `Clocks.fixedClock` in tests.

Shared domain values are not an automatic transport contract. For API requests,
responses, or persistence, use string fields produced by `format` and validate
with `tryParse` when receiving them, or supply equivalent explicit serializer
codecs. This library does not provide Fable.Remoting, Thoth, or System.Text.Json
integration. Do not assume default union serialization preserves its invariants.

The [SharedScheduling example](examples/SharedScheduling/Schedule.fs) is compiled
and tested on both runtimes. A complete SAFE application, browser startup, and
client/server transport are not currently covered by this repository's tests.
Client-side previews can also differ from server results if timezone databases
or clocks differ; let the server decide authoritative scheduling outcomes.

## Use

Reference `src/Fable.NodaTime/Fable.NodaTime.fsproj`. The JavaScript backend reads
`globalThis.Temporal` through Fable.Temporal. The library does not import a polyfill
or modify globals; the application chooses the implementation.

Native Temporal needs no setup. Otherwise, install `temporal-polyfill` and load it
in your JavaScript entry point **before importing the compiled F# application**:

```js
import 'temporal-polyfill/global';
await import('./App.js');
```

For Node, preload it with `node --import temporal-polyfill/global App.js`.
The runtime is captured when the library initializes, and initialization fails
with Fable.Temporal's missing-implementation error if Temporal is unavailable.
Any compatible implementation installed on `globalThis.Temporal` beforehand can
be used; `temporal-polyfill` is the setup used by this repository's tests.

```fsharp
open Fable.NodaTime

let zone = DateTimeZone.find "Australia/Melbourne"
let local = LocalDateTime.parse "2024-10-06T02:30:00"
let meeting = ZonedDateTime.resolve Resolvers.Lenient zone local

let tomorrow = meeting |> ZonedDateTime.plusDays Resolvers.Lenient 1
let after24Hours = meeting |> ZonedDateTime.plus (Duration.fromHours 24L)
let wire = meeting |> ZonedDateTime.format
```

[SharedScheduling](examples/SharedScheduling/Schedule.fs) shows a daily wall-clock
schedule using an injected `IClock`. `Clocks.fixedClock` makes it deterministic;
`Clocks.system` reads the host clock. A schedule using `Strict` fails on ambiguous
or skipped local times.

## Supported model

- `Instant`: a timeline point. `plus` adds elapsed time; `between start finish`
  returns signed elapsed time.
- `Duration`: arbitrary-precision signed nanoseconds. A day is exactly 24 hours.
- `LocalDate`, `LocalTime`, `LocalDateTime`: ISO dates and wall-clock times.
  `LocalDateTime.at`, `date`, and `time` combine and split them.
- `DateTimeZone`: a named IANA zone. `ZonedDateTime` pairs a zone with an instant;
  `ofInstant` projects it, `resolve` maps a local time, and `offsetSeconds` reads
  the offset.

Dates and instants are limited to years **0001–9999**, with nanosecond precision.
Constructors, arithmetic, and zone projections reject results outside that range.
Values have structural equality; equivalent fractional representations normalize
on input. Zone IDs are preserved, so aliases are distinct values. Compare zoned
values by their `instant` when only timeline identity matters.

`plusYears`, `plusMonths`, and `plusDays` operate on dates, local datetimes, and
zoned datetimes. Year/month changes clip to the last valid day; local time is
preserved. Zoned calendar operations require a resolver. Compose operations in
order, with each intermediate result validated:

```fsharp
LocalDate.parse "2024-02-29"
|> LocalDate.plusYears 1
|> LocalDate.plusMonths 1
// 2025-03-28: February clips before the month is added.
```

**Periods are deliberately unsupported.** There is no compound calendar duration,
period parser, or implicit order of units. Also excluded: non-ISO calendars,
standalone offsets, arbitrary fixed/custom zones, intervals, localized patterns,
Windows zone IDs, and leap-second timescales.

## Feature parity with NodaTime

This describes the current public API, not the full capabilities of either
backend. **Supported** means the listed operation is available on both runtimes;
**Partial** means a restricted subset or a material difference; **Not supported**
means there is no shared API for it. Platform-only conversions are marked separately.
No row implies complete NodaTime API or source compatibility.

| Feature | Status | Scope and differences from NodaTime |
| --- | --- | --- |
| Instant precision and elapsed arithmetic | Supported | Nanoseconds, `plus`, signed `between`, and `compare`; no rounding to ticks or milliseconds. Instant range is restricted as below. |
| ISO dates and local values | Partial | `LocalDate`, `LocalTime`, and `LocalDateTime`; creation, parsing, formatting, and date/time composition. No general component accessors, date adjusters, week-year APIs, or complete NodaTime member set. |
| Calendar and year range | Partial | ISO only, years 0001–9999. NodaTime's year zero, negative years, and other calendars are excluded. Results and zone projections must stay in range. |
| Calendar arithmetic | Partial | `plusYears`, `plusMonths`, `plusDays` match NodaTime clipping, in explicit call order. No direct weeks operation, local clock-unit arithmetic, or calendar differences/`Period.Between`. |
| Elapsed durations | Partial | Exact bigint nanoseconds, unit constructors, addition, and negation. The container is unbounded, unlike NodaTime Duration; native conversion enforces its range. No multiplication/division or full duration member set. A duration day is 24 hours. |
| Periods | Not supported | No Period type, builder, parsing, mixed-unit arithmetic, or period conversions. Compose single-unit date operations instead. |
| Named zones and instant projection | Partial | IANA lookup, instant/local projection, and offset in whole seconds. No custom providers, Windows IDs, arbitrary fixed-offset zones, or transition/zone-interval inspection. Named fixed-rule IANA zones such as `Etc/GMT+1` are accepted. |
| Timezone rule parity | Partial | .NET uses NodaTime TZDB; JavaScript uses the application's Temporal implementation. Rule versions, accepted IDs, aliases, and historical/future results can differ; no common database is bundled. |
| Strict and lenient local mapping | Supported | NodaTime `AtStrictly` / `AtLeniently` behavior: strict rejects gaps/overlaps; lenient picks the earlier overlap occurrence and shifts gaps forward by their full length. Temporal adapts these through `reject` / `compatible`; results depend on zone data. |
| Custom local mapping and start of day | Not supported | No `MapLocal` result, custom resolver delegates, independent overlap/gap policies, or `AtStartOfDay`. Resolving midnight is not a substitute for NodaTime's start-of-day operation. |
| Zoned arithmetic | Partial | Elapsed addition preserves the zone. Calendar helpers compose local arithmetic with an explicit resolver; these helpers are additions to this API, not native NodaTime ZonedDateTime calendar methods. |
| Offsets and offset date/time types | Not supported | No standalone Offset, OffsetDate, OffsetTime, or OffsetDateTime; only `ZonedDateTime.offsetSeconds`. |
| Intervals and other date types | Not supported | No Interval, DateInterval, AnnualDate, or YearMonth API. |
| Parsing and formatting | Partial | Fixed invariant wire grammar with nanoseconds. No NodaTime pattern objects, custom/localized formats, offset-bearing instant input, or calendar annotations. Zoned and duration wire formats are library-specific. |
| Equality and ordering | Partial | Canonical values have structural equality; zone aliases remain distinct IDs. Zoned equality includes the zone; compare extracted instants for timeline identity. Only Instant has an explicit comparison function; generated F# structural ordering is not a complete NodaTime comparison contract. |
| Clocks | Supported | `IClock`, system clock, and fixed clock. The clock interface is this library's own; no NodaTime.Testing clock utilities. Actual system-clock precision depends on the host. |
| Native NodaTime conversions | .NET only | `toX` / `fromX` for supported types. Reject non-ISO/out-of-range values and zones that do not equal the TZDB zone for their ID. Duration conversion is range-limited. |
| Native Temporal conversions | Fable only | `toX` / `fromX`; locals map to Plain types and zones to ID strings. Reject non-ISO/out-of-range values and numeric offset zones. Durations accept only time components (no calendar days), with exactness/range checks on output. |
| Error behavior | Partial | `tryParse`, `tryFind`, and `tryResolve` return the library's `TimeError`; other operations throw. Native exception classes/messages and NodaTime ParseResult are not mirrored. |
| Serialization integrations | Not supported | Portable string formats are provided, but no automatic JSON codecs, Fable.Remoting integration, database mappings, or native union wire contract. |
| NodaTime source compatibility | Not supported | Qualified F# modules and opaque shared values; existing code that opens NodaTime is not a drop-in client implementation. |
| End-to-end SAFE/browser integration | Not verified | Shared rules and package consumers are tested on .NET and Node using `temporal-polyfill`. No full SAFE app, browser matrix, or native-Temporal test matrix is included. |

## Is this basically just a NodaTime and Temporal wrapper?

Basically yes, but with a bit of logic to recreate NodaTime semantics on top of
Temporal. The Temporal adapter adds:

- **Strict and lenient local-time resolution:** map NodaTime's behavior to
  Temporal's disambiguation options, rejecting gaps and overlaps in strict mode,
  or choosing the earlier overlap occurrence and shifting gaps forward in lenient mode.
- **Calendar clipping:** preserve NodaTime's end-of-month behavior when adding
  years or months, with each operation applied and validated in order.
- **Elapsed durations:** use exact bigint nanoseconds and 24-hour days, keeping
  calendar units out of elapsed arithmetic.
- **Shared validation and wire formats:** enforce the supported ISO grammar and
  year range on both runtimes, preserve nanoseconds, and serialize zoned values
  as a UTC instant plus zone so overlap occurrences remain distinct.
- **F# value semantics:** store canonical strings or bigint nanoseconds so shared
  domain code can use structural equality without depending on backend objects.

The API covers a subset of NodaTime, and timezone database differences between
the runtimes still apply.

If you find any behavior differences to standard NodaTime, please [open an issue](https://github.com/olivercoad/Fable.NodaTime/issues).

## NodaTime / Temporal semantics

NodaTime defines the behavior of supported date arithmetic and local mapping.
The .NET backend delegates to NodaTime; the Temporal backend adapts its operations
to match. Shared validation defines the supported subset and wire format, rather
than changing NodaTime to follow Temporal.

- **Parsing:** Temporal accepts extra ISO forms and can ignore components when
  parsing local values. Shared validation restricts both backends to the wire
  grammar below, rejects year zero, offsets on locals, annotations, `24:00`,
  and leap seconds (which Temporal otherwise constrains to second 59).
- **Precision:** instant arithmetic uses bigint epoch nanoseconds on JavaScript
  and NodaTime nanoseconds on .NET. No conversion through floating-point seconds,
  JavaScript `Date`, or .NET ticks occurs. Host clocks may have lower resolution.
- **Calendar clipping:** each call applies one unit with end-of-month clipping.
  This avoids compound-period differences in clipping order and mixed signs.
- **DST:** `Strict` delegates to NodaTime `AtStrictly`: gaps and overlaps fail.
  `Lenient` delegates to `AtLeniently`: overlaps choose the earlier occurrence
  and gaps shift forward by their full length. The Temporal adapter implements
  these using `reject` and `compatible`, respectively. Custom NodaTime resolvers
  and separate earlier/later policies are outside the supported subset.
- **Range:** the common year range is checked after arithmetic and both instant
  and local zone projection. Duration values themselves are unbounded; native
  NodaTime duration conversion is bounded by NodaTime's range.

For New York's skipped `2024-03-10T02:30:00`, strict mapping fails and lenient
mapping produces 03:30. Fractional seconds and position within the gap are
retained, including half-hour transitions and skipped whole days. Calendar days
can therefore be 23 or 25 elapsed hours.

The portable year range and wire grammar are deliberately narrower than NodaTime.
The bigint duration container is unbounded, while conversion to native NodaTime
Duration enforces its range. Zoned calendar helpers compose local arithmetic and
resolution; they are conveniences, not native NodaTime ZonedDateTime methods.
These are explicit API boundaries, not Temporal semantics imposed on NodaTime.

Reference semantics: [Temporal date parsing and arithmetic](https://tc39.es/proposal-temporal/docs/plaindate.html),
[Temporal disambiguation](https://tc39.es/proposal-temporal/docs/ambiguity.html),
and [NodaTime resolvers](https://nodatime.org/3.2.x/api/NodaTime.TimeZones.Resolvers.html).

**Timezone data is not unified.** .NET uses NodaTime's bundled TZDB; JavaScript
uses the selected Temporal implementation's timezone data (host Intl/ICU for
native Temporal and the test polyfill). Rules and accepted aliases can differ.
`DateTimeZone.dataVersion` reports TZDB on .NET and explicitly reports an unknown
implementation version on JavaScript. Use correctly cased canonical IANA IDs. Persist a
resolved instant and zone for an occurrence, or local time, zone, and resolver
for a recurring schedule. Resolve authoritatively on the server when identical
results across database versions are essential.

## Serialization and errors

Use `format` / `parse`, not default union serialization. JSON fields should be
strings, including durations:

```text
Instant        2024-01-01T00:00:00.123456789Z
LocalDate      2024-01-01
LocalTime      12:34:56.123456789
LocalDateTime  2024-01-01T12:34:56.123456789
Duration       -9007199254740993
ZonedDateTime  2024-11-03T06:30:00.000000000Z[America/New_York]
```

Time-bearing output always has nine fractional digits; input accepts seconds
with an optional 1–9 digit fraction. Instant input requires uppercase `T` and
UTC `Z`; dates require four-digit years. Duration input is a decimal integer
without a leading `+` or redundant zeros (`-0` normalizes to `0`). Zoned strings
are the library's **UTC instant + zone** format, not Temporal's local-offset
format; they preserve the exact overlap occurrence. Store a zone alone using
`DateTimeZone.id` and read it using `find`.

`tryParse`, `DateTimeZone.tryFind`, and `ZonedDateTime.tryResolve` return
`Result<_, TimeError>`. Their convenience counterparts and arithmetic throw on
invalid input or overflow. Error cases are portable; exception details are not
part of the wire contract.

The .NET-only `NodaTimeConversions` module provides `toX` / `fromX` for every
supported value type. Conversions preserve nanoseconds and reject non-ISO dates,
unsupported ranges, and zones that do not equal the TZDB provider's zone for
their ID. Keep these calls at the .NET boundary.

The Fable/JavaScript-only `TemporalConversions` module provides the same `toX` /
`fromX` names for Fable.Temporal values. It uses the application's configured
Temporal implementation and does not load a polyfill:

```fsharp
#if FABLE_COMPILER
let temporal: Fable.Temporal.Instant = TemporalConversions.toInstant instant
let shared: Instant = TemporalConversions.fromInstant temporal
#endif
```

`toLocalDate`, `toLocalTime`, and `toLocalDateTime` return Temporal `PlainDate`,
`PlainTime`, and `PlainDateTime`; the corresponding `from` functions accept them.
Timezone conversions use IANA identifier strings because Temporal has no standalone
timezone object. Zoned conversions preserve the instant, including either
occurrence in an overlap. Non-ISO calendars, unsupported date ranges, and numeric
fixed-offset zones are rejected.

Duration conversions preserve exact nanoseconds. `fromDuration` accepts only time
components: nonzero years, months, weeks, or days are rejected because calendar
units require a relative date/zone to determine elapsed time. `toDuration` emits
a time-only Temporal duration and throws if the value exceeds Temporal's range
or cannot be represented exactly. Component balancing need not round-trip; the
elapsed nanoseconds do.

## Development

Requires .NET 10 SDK, .NET 8 runtime, Node 24+, and pnpm 10.32.1:

```sh
dotnet tool restore
pnpm install --frozen-lockfile
pnpm test
pnpm run format:check
dotnet build Fable.NodaTime.slnx -c Release
pnpm run test:package
```

The same F# behavioral tests run on .NET and Fable/Node, including DST transitions,
precision, clipping, invalid inputs, boundaries, serialization, and the example.
Each runtime additionally tests its native conversion module. Browser integration
is not tested.
`test:package` packs into a local feed and runs the suite through package references
on both runtimes using an isolated cache. It publishes nothing. `pnpm run format`
formats the maintained F# sources with Fantomas.

`Types.fs` defines opaque values and contracts. `Api.fs` owns portable validation,
composition, and serialization. The two `Backend.*.fs` files implement runtime
operations; `NodaTime.fs` and `Temporal.fs` contain platform-specific conversions. Strings hold canonical ISO
values, keeping equality and serialization identical without exposing runtime
objects. Durations hold bigint nanoseconds. Backend choice is internal.

Library dependencies are pinned: NodaTime 3.3.2 and Fable.Temporal 0.1.0.
The test harness preloads `temporal-polyfill` 1.0.6 as a development dependency;
consumers choose their own Temporal implementation.
No sibling checkout is needed. `Directory.Build.props` selects the FSharp.Core
version required by Fable.Temporal. Fable.Package.SDK packs F# sources and XML
docs; Ionide.KeepAChangelog.Tasks reads the initial package version from
[CHANGELOG.md](CHANGELOG.md). Its version heading is local packaging metadata,
not a published release. Both are private build dependencies.
