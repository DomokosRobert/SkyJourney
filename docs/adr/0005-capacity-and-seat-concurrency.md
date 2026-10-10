# ADR-0005: Capacity and seat concurrency control

## Context
Two users may book the last seats or pick the same seat at the same moment. Read-then-write checks
in application code are racy.

## Decision
1. **Capacity:** `Flight.SeatsSold` is changed only by an atomic conditional `UPDATE … WHERE seats_sold + n <= capacity`
   (EF Core `ExecuteUpdateAsync`). Zero affected rows means sold out. It runs in the booking transaction,
   so a failed booking insert rolls the counter back.
2. **Seats:** a partial unique index on `seat_reservations (flight_id, aircraft_seat_id) WHERE status IN ('Held','Confirmed')`.
   The database is the final arbiter. Unique violations are translated to `SeatUnavailableException` → HTTP 409.
3. **Booking aggregate:** PostgreSQL `xmin` system column as an optimistic concurrency token for
   state transitions (payment vs. expiry vs. cancellation).
4. Holds expire with the booking (20 minutes). A background service expires stale bookings every
   minute. Seat assignment also releases expired holds for the flight inside its own transaction.

## Alternatives
- `SELECT … FOR UPDATE` on the flight row: correct, but it serialises all bookings on a flight and holds locks longer.
- Serializable isolation: correct, but it needs retry loops everywhere.

## Consequences
Short transactions and DB-enforced invariants. Tests run concurrent requests against real PostgreSQL to prove it.
