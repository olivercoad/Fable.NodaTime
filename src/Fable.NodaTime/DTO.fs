namespace Fable.NodaTime

/// Transport representation of an instant. Validate with fromNodaDTO before using in domain code.
[<RequireQualifiedAccess>]
type InstantDTO = { Value: string }

/// Transport representation of an ISO date.
[<RequireQualifiedAccess>]
type LocalDateDTO = { Value: string }

/// Transport representation of a local time.
[<RequireQualifiedAccess>]
type LocalTimeDTO = { Value: string }

/// Transport representation of an ISO local date and time.
[<RequireQualifiedAccess>]
type LocalDateTimeDTO = { Value: string }

/// Transport representation of exact elapsed nanoseconds, stored as a decimal string.
[<RequireQualifiedAccess>]
type DurationDTO = { Value: string }

/// Transport representation of an IANA timezone identifier.
[<RequireQualifiedAccess>]
type DateTimeZoneDTO = { Value: string }

/// Transport representation of a UTC instant plus timezone, preserving overlap identity.
[<RequireQualifiedAccess>]
type ZonedDateTimeDTO = { Value: string }

module private DTOValidation =
    let read getValue parse dto =
        if obj.ReferenceEquals(dto, null) then
            Error(InvalidValue "Date/time DTO cannot be null")
        else
            parse (getValue dto)

/// Supports the inline conversion helpers; application code should use toNodaDTO and fromNodaDTO.
[<System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)>]
type NodaDTOConversions =
    static member ToDTO(value: Instant) : InstantDTO =
        {
            InstantDTO.Value = Instant.format value
        }

    static member ToDTO(value: LocalDate) : LocalDateDTO =
        {
            LocalDateDTO.Value = LocalDate.format value
        }

    static member ToDTO(value: LocalTime) : LocalTimeDTO =
        {
            LocalTimeDTO.Value = LocalTime.format value
        }

    static member ToDTO(value: LocalDateTime) : LocalDateTimeDTO =
        {
            LocalDateTimeDTO.Value = LocalDateTime.format value
        }

    static member ToDTO(value: Duration) : DurationDTO =
        {
            DurationDTO.Value = Duration.format value
        }

    static member ToDTO(value: DateTimeZone) : DateTimeZoneDTO =
        {
            DateTimeZoneDTO.Value = DateTimeZone.id value
        }

    static member ToDTO(value: ZonedDateTime) : ZonedDateTimeDTO =
        {
            ZonedDateTimeDTO.Value = ZonedDateTime.format value
        }

    static member FromDTO(value: InstantDTO) =
        DTOValidation.read (fun (dto: InstantDTO) -> dto.Value) Instant.tryParse value

    static member FromDTO(value: LocalDateDTO) =
        DTOValidation.read (fun (dto: LocalDateDTO) -> dto.Value) LocalDate.tryParse value

    static member FromDTO(value: LocalTimeDTO) =
        DTOValidation.read (fun (dto: LocalTimeDTO) -> dto.Value) LocalTime.tryParse value

    static member FromDTO(value: LocalDateTimeDTO) =
        DTOValidation.read (fun (dto: LocalDateTimeDTO) -> dto.Value) LocalDateTime.tryParse value

    static member FromDTO(value: DurationDTO) =
        DTOValidation.read (fun (dto: DurationDTO) -> dto.Value) Duration.tryParse value

    static member FromDTO(value: DateTimeZoneDTO) =
        DTOValidation.read (fun (dto: DateTimeZoneDTO) -> dto.Value) DateTimeZone.tryFind value

    static member FromDTO(value: ZonedDateTimeDTO) =
        DTOValidation.read (fun (dto: ZonedDateTimeDTO) -> dto.Value) ZonedDateTime.tryParse value

[<AutoOpen>]
module NodaDTO =
    /// Convert a supported domain value to its corresponding typed transport record.
    let inline toNodaDTO (value: ^T) : ^DTO =
        ((^T or NodaDTOConversions): (static member ToDTO: ^T -> ^DTO) value)

    /// Validate a typed transport record and return the corresponding domain value or TimeError.
    let inline fromNodaDTO (value: ^DTO) : Result< ^T, TimeError > =
        ((^DTO or NodaDTOConversions): (static member FromDTO: ^DTO -> Result< ^T, TimeError >) value)
