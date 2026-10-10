namespace SkyJourney.Domain.Bookings;

public enum BookingStatus
{
    PendingPayment,
    Confirmed,
    Cancelled,
    Expired,
}

public enum JourneyDirection
{
    Outbound,
    Return,
}

public enum PassengerType
{
    Adult,
    Child,
}

public enum SeatReservationStatus
{
    Held,
    Confirmed,
    Released,
}

public enum PaymentStatus
{
    Pending,
    Succeeded,
    Failed,
    PartiallyRefunded,
    Refunded,
}

public enum BookingActor
{
    Customer,
    Administrator,
    System,
}
