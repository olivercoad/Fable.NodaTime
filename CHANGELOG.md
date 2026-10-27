# Changelog

## [Unreleased]

### Added

- Optional Fable.NodaTime.TypedJson package with validated codecs and registry support, a usage guide, and .NET/JavaScript package consumer tests.
- Opt-in System.Text.Json converters for all seven shared value types, using validated strings without additional package dependencies.
- Optional Fable.NodaTime.Thoth package with backend-independent Thoth.Json.Core codecs for domain types.
- Typed DTO variants with qualified record fields, inline generic conversion functions, and a Fable.Remoting example with boundary validation.
- SAFE serialization documentation and tests exchanging JSON between .NET and JavaScript, including precision, DST overlaps, and invalid input.

### Changed

- Standardized library, example, and test projects on .NET 10.

## [0.1.0] - 2026-10-24

### Added

- Shared ISO date/time values backed by NodaTime on .NET and Fable.Temporal in JavaScript.
- Exact nanosecond durations and explicit calendar year, month, and day arithmetic.
- Named timezones and explicit DST resolution, including whole-gap shifting.
- Portable serialization, validated NodaTime and Temporal conversions, and injectable clocks.
- A shared scheduling example and behavioral tests on .NET and JavaScript.
- NuGet consumer tests on both runtimes and pinned Fantomas formatting.
- Changelog-driven package metadata with Ionide.KeepAChangelog.Tasks and F# source packaging with Fable.Package.SDK.
