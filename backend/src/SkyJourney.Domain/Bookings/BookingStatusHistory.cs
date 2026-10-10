using SkyJourney.Domain.Common;

namespace SkyJourney.Domain.Bookings;

public sealed class BookingStatusHistory : Entity
{
    private BookingStatusHistory()
    {
    }

    internal BookingStatusHistory(Guid bookingId, BookingStatus? from, BookingStatus to, BookingActor actor, string? reason, DateTimeOffset at)
    {
        BookingId = bookingId;
        FromStatus = from;
        ToStatus = to;
        Actor = actor;
        Reason = reason;
        ChangedAt = at;
    }

    public Guid BookingId { get; private set; }

    public BookingStatus? FromStatus { get; private set; }

    public BookingStatus ToStatus { get; private set; }

    public BookingActor Actor { get; private set; }

    public string? Reason { get; private set; }

    public DateTimeOffset ChangedAt { get; private set; }
}
