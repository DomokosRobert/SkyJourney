# ADR-0006: UTC instants + IANA time zones + local departure date

## Context
Flights depart and arrive in different time zones, and DST changes offsets twice a year. Users search
by *local* departure date.

## Decision
- Instants are stored as `timestamptz` (`DateTimeOffset`, always UTC; Npgsql enforces offset 0).
- `Airport.TimeZoneId` is an IANA ID. .NET on Linux/macOS resolves IANA natively, and Windows works through ICU.
- `FlightSchedule` stores a local `TimeOnly` departure and a duration. Generating an instance converts
  local → UTC for that date with `TimeZoneInfo`, so DST-correct UTC times differ across the year.
- `Flight.DepartureLocalDate` (`date`) is denormalised for fast, unambiguous search by local date.
- The API returns UTC plus the local `DateTimeOffset` with the airport's offset at that instant.
- `TimeProvider` is injected, never `DateTime.UtcNow`.

## Consequences
Clients display times as given. Arrival can be "+1 day" or even earlier in local terms than departure. The UI handles that.
