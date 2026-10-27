namespace SerializationExample

open Fable.NodaTime

type Booking =
    {
        StartsAt: Instant
        Length: Duration
        Occurrence: ZonedDateTime
    }

/// Typed transport fields; domain values are validated at each receiving boundary.
type BookingDto =
    {
        StartsAt: InstantDTO
        Length: DurationDTO
        Occurrence: ZonedDateTimeDTO
    }

type IBookingApi =
    {
        Save: BookingDto -> Async<Result<BookingDto, string>>
    }

[<RequireQualifiedAccess>]
module BookingDto =
    let ofDomain (booking: Booking) : BookingDto =
        {
            StartsAt = toNodaDTO booking.StartsAt
            Length = toNodaDTO booking.Length
            Occurrence = toNodaDTO booking.Occurrence
        }

    let toDomain (dto: BookingDto) : Result<Booking, string> =
        let field name result =
            result |> Result.mapError (fun error -> sprintf "%s: %A" name error)

        if obj.ReferenceEquals(dto, null) then
            Error "Booking cannot be null"
        else
            fromNodaDTO dto.StartsAt
            |> field "StartsAt"
            |> Result.bind (fun instant ->
                fromNodaDTO dto.Length
                |> field "Length"
                |> Result.bind (fun duration ->
                    fromNodaDTO dto.Occurrence
                    |> field "Occurrence"
                    |> Result.map (fun occurrence ->
                        {
                            StartsAt = instant
                            Length = duration
                            Occurrence = occurrence
                        })))

[<RequireQualifiedAccess>]
module BookingApi =
    /// Use as the implementation passed to Remoting.fromValue on the server.
    let create (save: Booking -> Async<Booking>) : IBookingApi =
        {
            Save =
                fun dto ->
                    async {
                        match BookingDto.toDomain dto with
                        | Error error -> return Error error
                        | Ok booking ->
                            let! saved = save booking
                            return Ok(BookingDto.ofDomain saved)
                    }
        }
