# Learning Guide

This guide walks through the ideas SkyJourney is built on, in the order you meet them when a customer books a flight.
Each section explains the problem, how this codebase solves it, and where to read the code. Paths are relative to the
repository root. Backend paths start at `backend/src/`, and mobile paths at `mobile/SkyJourney.Mobile/src/`.

Suggested route: read [architecture.md](architecture.md) first, then follow the sections below with the code open. Then
run one integration test in the debugger, for example `EndToEnd/JourneyTests`.

---

## 1. Architecture: a layered modular monolith
**Problem:** keep business rules testable and independent of HTTP and the database, without the overhead of
microservices.

**Solution:** four projects, each depending only on the ones beneath it:
`Api → Application → Domain`, with `Infrastructure` implementing the Application's interfaces.
- `SkyJourney.Domain`: entities and rules with no framework dependencies (`Booking`, `Flight`, `Money`, `FareConditions`).
- `SkyJourney.Application`: use cases (`BookingService`, `PaymentService`, `FlightSearchService`) and validators.
- `SkyJourney.Infrastructure`: EF Core, Identity/JWT, the simulated payment gateway, background jobs.
- `SkyJourney.Api`: minimal-API endpoints (`Endpoints/*.cs`), error mapping, auth configuration.

The Application layer uses `ISkyJourneyDbContext` directly instead of repositories
([ADR-0004](adr/0004-data-access-without-repositories.md)). EF Core already is a unit of work, and integration tests
against real PostgreSQL replace mocked repositories.

**Exercise:** follow `POST /api/v1/bookings` from `BookingEndpoints.cs` to `BookingService.CreateAsync` and into
`Booking.Create`.

## 2. Time zones and deterministic data
**Problem:** a flight departs at 07:30 *local time* every Monday, but instants must be compared in UTC, and DST moves
the UTC time twice a year.

**Solution** ([ADR-0006](adr/0006-time-representation.md)):
- Schedules store a local `TimeOnly`. Each generated `Flight` stores UTC instants plus the local departure date for
  searching (`Domain/Common/TimeZoneConversion.cs`, `Domain/Flights/FlightSchedule.cs`).
- Code never calls `DateTime.UtcNow`. `TimeProvider` is injected everywhere, so tests freeze the clock at
  2026-11-02 06:00 UTC.
- Demo flights come from `DeterministicFlightDataProvider` through the `IFlightDataProvider` abstraction
  ([ADR-0007](adr/0007-flight-data-provider-abstraction.md)). `StableHash` derives prices and load factors from the
  flight number and date, so every machine generates identical data. A real data feed could replace the provider
  without touching the booking code.

## 3. Money and fare snapshots
**Problem:** prices change, and a customer must keep what they paid for.

**Solution:**
- `Money` is a value object: a decimal amount plus a currency, with rounding and same-currency checks
  (`Domain/Common/Money.cs`).
- The **server** prices every booking (`FarePricing`, `SeatPricing`). The client's total is only an estimate.
- Each `BookingSegment` copies the fare price and `FareConditions` (baggage, seat selection, refund %) at booking
  time. Refunds use that snapshot, not today's fare.

## 4. Authentication: short JWTs and rotating refresh tokens
[ADR-0003](adr/0003-authentication-jwt-and-rotating-refresh-tokens.md)
- ASP.NET Core Identity hashes passwords and handles lockout. `AccessTokenIssuer` signs a 15-minute JWT.
- The refresh token is 256 random bits. The server stores only its SHA-256 hash (`RefreshTokenSecret`).
- Every refresh **rotates** the token. Presenting an old token again revokes the whole token family, which detects a
  stolen token being replayed (`Infrastructure/Identity/AuthService.cs`).
- On the phone, the refresh token lives in SecureStore (Keychain) and the access token only in memory
  (`auth/tokenStorage.ts`, `auth/session.ts`). `createSessionManager` makes concurrent 401s share **one** refresh call.
  Its tests drive that race with deferred promises.

## 5. Concurrency: never sell the same seat twice
[ADR-0005](adr/0005-capacity-and-seat-concurrency.md). Three mechanisms, one per kind of conflict:

| Conflict | Mechanism | Where |
|----------|-----------|-------|
| More bookings than seats | One atomic `UPDATE flights SET seats_sold = seats_sold + n WHERE seats_sold + n <= capacity`. Zero rows updated means sold out. | `Application/Bookings/FlightCapacity.cs` |
| Two people pick seat 12A | A partial unique index on `(flight_id, aircraft_seat_id)` for held or confirmed reservations. The database rejects the second insert, which becomes a 409. | `Persistence/Configurations/BookingConfigurations.cs` |
| Payment vs. expiry vs. cancellation | Optimistic concurrency on the booking row through PostgreSQL's `xmin` column | `Booking` mapping, `PaymentService` |

Why not `SELECT … FOR UPDATE`? It is correct, but it serialises every booking on a flight. The conditional update holds
its row lock for microseconds.

**Proof:** `IntegrationTests/Bookings` races 10 users for the last 3 seats and 6 users for one seat, through the
real HTTP pipeline against real PostgreSQL.

## 6. Holds and background jobs
A new booking holds its places for 20 minutes (`PendingPayment`). `BookingExpiryJob` releases lapsed holds every minute.
Booking, seat assignment and cancellation also expire due bookings on the affected flight first, so a lapsed hold never
blocks a seat until the next sweep. `FlightHorizonJob` generates flights for new days as time passes. Both jobs derive
from `RecurringJob` and are safe to run on several instances.

## 7. Idempotency and simulated payments
[ADR-0008](adr/0008-simulated-payments-and-idempotency.md)
**Problem:** on mobile networks a response can be lost after the server acted. Retrying must not book or charge twice.
- `POST /bookings` and `POST /bookings/{ref}/payments` require an `Idempotency-Key`. Unique indexes allow one result per
  key. A replay returns the stored result, and the same key with a different body returns 422.
- The app keeps the key for a retry after a *lost response*, and makes a new one after a *decline*
  (`booking/idempotency.ts`, `booking/payments.ts`).
- Payment runs in two phases: save a `Pending` attempt, call the gateway **outside** any transaction, then record the
  outcome. Holding a transaction open across a network call would block every other change to the booking.
- `SimulatedPaymentGateway` accepts only test tokens. A decline is a recorded business result (`201`, `status: Failed`),
  not an HTTP error.

## 8. API design and errors
- Minimal APIs under `/api/v1` ([ADR-0002](adr/0002-rest-minimal-apis-and-url-versioning.md)), with FluentValidation via
  `ValidationFilter`.
- Every error is RFC 9457 Problem Details with a stable `code` and the request's `X-Correlation-Id`
  (`GlobalExceptionHandler`, `CorrelationIdMiddleware`). The app maps them to friendly messages (`api/client.ts`).
- Serilog writes one structured log line per request. Health checks: `/health/live` and `/health/ready`.

## 9. The mobile app
[ADR-0009](adr/0009-mobile-stack.md)
- **Expo Router:** file-based routes in `src/app`. Tabs (Search, Trips, Profile) and modal pickers.
- **TanStack Query** owns server state: caching, retries, and refetching on reconnect or focus (`api/queryClient.ts`,
  `lib/network.ts`). React context holds only the booking draft (`booking/BookingDraftContext.tsx`).
- **React Hook Form + Zod:** form validation (`auth/schemas.ts`, `booking/passengers.ts`).
- **UI states:** every screen has loading, empty, error and offline states (`components/StateViews.tsx`,
  `OfflineBanner.tsx`). The hold countdown is in `booking/HoldBanner.tsx`.
- **API address:** `api/config.ts` uses `EXPO_PUBLIC_API_URL` if set; in development it uses the Metro host's IP, so a
  physical phone finds your Mac.

## 10. Testing strategy
See [testing.md](testing.md). In short:
- **Unit tests** cover pure rules: money, DST, the state machine, refunds.
- **Integration tests** run the real API in memory against a throwaway PostgreSQL (Testcontainers) with a frozen
  clock. Each test that changes data uses its own route and date, so tests can run in parallel.
- **End-to-end:** journey tests plus `scripts/e2e_smoke.py` against a running server.
- **Mobile:** logic tested as plain TypeScript with injected fakes, and screens rendered inside the real providers.

## 11. Delivery
- **Docker:** a multi-stage `backend/Dockerfile` builds a non-root runtime image and an EF Core **migrations bundle**
  image. Migrations run as a separate step before rollout, never implicitly in production.
- **CI:** `.github/workflows/ci.yml` runs the backend, Docker and mobile checks. Dependabot keeps actions and NuGet
  packages current.
- **EAS:** `eas.json` and `app.config.ts` define development, preview and production builds with placeholder
  identifiers. See [deployment.md](deployment.md).

---

## Ideas for practice
1. Add a "change seat after payment" feature. Think about which concurrency mechanism protects it.
2. Add one-stop connections. Today a booking has at most one `BookingSegment` per direction (a unique index on
   `(booking_id, direction)`). Connections need a leg sequence number and a minimum connection time.
3. Replace `SimulatedPaymentGateway` with Stripe test mode behind the same `IPaymentGateway`.
4. Write Maestro flows for search → booking → payment once Maestro is installed.

## Known limitations
- **Mobile UI end-to-end tests are not automated.** Maestro is not installed. The screens are covered by jest screen
  tests and were checked by hand on the simulator, but not every signed-in flow was tapped through end to end.
- **The CI workflow has never run on GitHub** (nothing has been pushed).
- **Nothing is deployed, and no EAS build exists.** Identifiers and API URLs are placeholders.
- **No admin UI.** The admin API is used through Swagger or `SkyJourney.Api.http`.
- **Racing schedule creation:** two schedules for the same flight number created at the same instant could both pass the
  overlap check (see the M15 log entry).
- **Forwarded-header handling** is opt-in and has no automated test.
- **Data Protection keys** are ephemeral; nothing uses them today.
- **Startup without configuration:** starting the API outside Development without a connection string ends with an
  unhandled exception and stack trace, not a friendly message.
- **Out of scope by design:** connections, infants, add-on bags, changes to a confirmed booking, e-mail and push
  notifications, real payments, real flight data.
