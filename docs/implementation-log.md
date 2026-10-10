# Implementation Log

One entry per milestone, newest last. A commit cannot contain its own hash, so each entry's hash
is filled in by the next commit (or listed in the final report).

| Legend | |
|---|---|
| ✅ | check ran and passed |
| ⚠️ | check ran with a documented limitation |
| ⛔ | check could not run (reason given) |

---

## M1 — `docs: define SkyJourney scope and architecture`
- **Content:** implementation plan, architecture, ADR-0001…0009, this log.
- **Validation:** documentation only. Mermaid diagrams reviewed manually.
- **Findings:** port 5432 is occupied on the dev machine, so compose uses 5433. Maestro and `psql` are not installed.

## M2 — `chore: establish repository and development tooling`
- **Previous commit:** M1 = `74b4b4e`.
- **Content:** `.gitignore`, `.gitattributes`, `.editorconfig`, `docker-compose.yml` (PostgreSQL 17 on host port 5433 with a health check and named volume), `.env.example`.
- **Validation:** ✅ `docker-compose config -q`; ✅ `docker-compose up -d`, container healthy.
- **Note:** this machine has the standalone `docker-compose` binary (v5), not the `docker compose` CLI plugin. The docs show both forms.

## M3 — `feat(backend): create API and application foundation`
- **Previous commit:** M2 = `e30367e`.
- **Content:** `backend/SkyJourney.sln` with Domain/Application/Infrastructure/Api projects; central package management;
  `TreatWarningsAsErrors`; `global.json` (SDK 10.0.100+, rollForward latestFeature); repo-local `nuget.config` (nuget.org only);
  local `dotnet-ef` tool; Serilog (compact JSON in production, readable in development); correlation-ID middleware;
  `IExceptionHandler` → RFC 9457 problem details with a stable `code`; FluentValidation endpoint filter; `/health/live`, `/health/ready`;
  OpenAPI document + Swagger UI (development only).
- **Validation:** ✅ `dotnet build` (0 warnings); ✅ manual smoke test: `/health/ready` 200 against compose PostgreSQL,
  `/openapi/v1.json` served, unknown route → `application/problem+json` 404 with echoed `X-Correlation-Id`.
- **Package policy:** each package is pinned to the newest stable release that is at least two weeks old.
- **Finding:** the machine-wide NuGet config includes a private corporate feed. The repo `nuget.config` clears it.

## M4 — `feat(database): configure PostgreSQL, domain model and initial migration`
- **Previous commit:** M3 = `df9346b`.
- **Content:** domain model (airports, fleet and seat configuration, schedules, flight instances, fares, booking aggregate with
  state machine, seats, payments, status history); `Money` and `FareConditions` mapped as EF Core complex types; snake_case naming;
  enums stored as strings; `InitialSchema` migration with FKs, explicit delete behaviours, unique and partial unique indexes,
  check constraints (`seats_sold <= capacity`) and `xmin` concurrency on bookings; design-time DbContext factory;
  migrate-on-startup in Development only; unit-test project (xUnit v3 on Microsoft Testing Platform).
- **Validation:** ✅ `dotnet build` (0 warnings); ✅ `dotnet ef database update` against compose PostgreSQL (21 tables);
  ✅ `dotnet test`: 44/44 domain unit tests (money, DST conversion, schedule generation, booking lifecycle, seats, refunds).
- **Decision:** xUnit v3 4.x requires Microsoft Testing Platform under the .NET 10 SDK, so `global.json` opts in (`test.runner`).

## M5 — `feat(flights): implement airport and flight search with seed data`
- **Previous commit:** M4 = `a1ba3e4`.
- **Content:** `IFlightDataProvider` abstraction and `DeterministicFlightDataProvider` (30 real airports, the fictional
  airline SkyJourney Air `SJ`, 3 aircraft types, 100 schedules derived from great-circle distance and a stable hash);
  idempotent `FlightDataImporter` (reference data + schedule → flight generation); startup seeding behind
  `FlightData:SeedOnStartup` (on in Development only); endpoints `GET /api/v1/airports`, `/airports/{iata}`,
  `/airports/{iata}/destinations`, `/flights/search` (one-way/return, passengers, sort, departure-time window, max price),
  `/flights/{id}`, `/flights/lowest-fares`; `SkyJourney.Api.http` sample requests.
- **Validation:** ✅ `dotnet build` (0 warnings); ✅ `dotnet test` 44/44; ✅ manual smoke test against compose PostgreSQL:
  first start imported 30 airports / 100 schedules and generated 8,632 flights (~3 s); a restart generated 0 (idempotent);
  search returned local + UTC times with offsets across a DST-different pair (BUD→LHR); 400 ProblemDetails for invalid
  input and past dates; 404 for unknown airports; OpenAPI document lists all six routes.
- **Decisions:** results expose `seatsLeft` only when ≤ 9 seats remain; children pay the adult fare (documented simplification);
  `sort` is a case-insensitive string because minimal-API enum binding is case-sensitive; the return leg is validated against
  the outbound arrival (minimum stay) at booking time, not in search.
- **Deferred:** a periodic job that extends the flight horizon (lands with the background jobs in M12).

## M6 — `test(flights): add flight search unit and integration tests`
- **Previous commit:** M5 = `a90a3e0`.
- **Content:** unit tests for the search validator, fare pricing and the deterministic data set (determinism, unique keys,
  bidirectional routes, every schedule passes domain validation); new `SkyJourney.IntegrationTests` project
  (`WebApplicationFactory` + Testcontainers PostgreSQL 17 assembly fixture, frozen `FakeTimeProvider`) covering one-way/return
  search, local/UTC times, sorting, time-window filter, sales cutoff, capacity filtering, cancelled flights, unavailable fares,
  404/400 Problem Details, flight details, lowest-fare calendar, airport ranking, destinations and importer idempotency;
  `docs/testing.md`.
- **Validation:** ✅ `dotnet build` (0 warnings); ✅ unit tests 76/76; ✅ integration tests 22/22 (with
  `TESTCONTAINERS_DOCKER_SOCKET_OVERRIDE=/var/run/docker.sock`, see below).
- **Finding:** this machine runs Docker through Colima. Ryuk's socket bind mount fails unless the override variable points at
  the in-VM socket path. Documented in `docs/testing.md`; CI (Linux) does not need it.

## M7 — `feat(mobile): initialize Expo app, design system and navigation`
- **Previous commit:** M6 = `c28e691`.
- **Content:** Expo SDK 57 app in `mobile/SkyJourney.Mobile` (from `create-expo-app` blank-typescript template; React Native 0.86,
  React 19.2, TypeScript 6, strict + `noUncheckedIndexedAccess`); Expo Router with typed routes, `skyjourney` scheme and a
  Search / Trips / Profile tab layout; theme tokens and design-system components (AppText, Button, Card, TextField, Screen,
  Loading/Empty/Error states, OfflineBanner); typed fetch client mapping Problem Details to `ApiError` with timeout and
  pluggable auth hooks (used in M10); runtime API base URL resolution; TanStack Query client wired to NetInfo and app focus;
  airport-local date/time formatting helpers; Jest (jest-expo) + React Native Testing Library; ESLint (eslint-config-expo).
  The search tab currently shows an API connectivity check; Trips and Profile show honest empty states until their milestones.
- **Validation:** ✅ `npm run typecheck`; ✅ `npm run lint`; ✅ `npm test` 18/18 (URL resolution, formatting, API client
  error mapping and token-refresh retry, Button); ✅ `npx expo-doctor` 21/21; ✅ `npx expo export --platform ios` bundles.
  Not yet run on a simulator/device (needs the search UI from M8 to be meaningful).
- **Decisions:**
  - Package policy (no releases younger than two weeks): `expo` pinned to 57.0.25 (57.0.26/27 are newer) and excluded from
    expo-doctor's version check via `expo.install.exclude`; ESLint pinned to 9.39.5 because `expo lint` requires ESLint 9;
    Jest 29.7 / `@types/jest` 29.5 as expected by the SDK; `@react-native/jest-preset` 0.86.3 and `test-renderer` 1.3.0 added
    as required peers of jest-expo 57 and RNTL 14.
  - Removed template files that do not belong to this project: the template's `.claude/` settings (enabled a third-party
    plugin), `LICENSE`, `App.tsx`, `index.ts`. Kept the template `AGENTS.md` (Expo-specific agent guidance).
  - TypeScript 6 no longer auto-includes all `@types`, so `tsconfig.json` lists `"types": ["jest"]`.

## Fix — `fix(api): log request outcome after exception handling`
- **Found during** M8 simulator testing: when Expo Go was terminated mid-request, the request log recorded `500 ERR` with an
  `OperationCanceledException` stack, because Serilog request logging ran *inside* the exception handler and so never saw the
  final status. The handler's own log lines also lacked the correlation id.
- **Content:** correlation-id middleware and request logging now wrap `UseExceptionHandler`; `RequestLogLevel` logs client
  disconnects (499) at Debug, 5xx/exceptions at Error and everything else at Information; integration tests for correlation-id
  echo (generated, preserved, unsafe value replaced, present in Problem Details body) and the level mapping.
- **Validation:** ✅ `dotnet build` (0 warnings); ✅ `dotnet test` 106/106. Manually: request log lines carry the caller's
  correlation id and record final status codes. A real mid-request client abort could not be reproduced locally (responses take
  ~1 ms); the 499 path is covered by the level-mapping test only.

## M8 — `feat(mobile): implement flight search experience`
- **Previous commit:** M7 = `1a6e518` (followed by fix `8cb74cf`).
- **Content:** typed wire models and TanStack Query hooks for airports, destinations, search, lowest fares and flight details;
  search form (one-way/return, swap, passenger steppers, zod validation); airport picker modal with debounced search and
  "destinations from origin" when the query is empty; price calendar modal (lowest fare per day, cheapest day highlighted,
  days without flights disabled, return calendar uses the reverse route); results screen with previous/next day, sorting,
  departure-time and price filters, pull-to-refresh and loading/empty/error/offline states; flight details with fare families
  and their conditions; return-leg selection; trip summary with an indicative total. Booking-draft React context holds the
  criteria and selected fares.
- **Validation:** ✅ `npm run typecheck`; ✅ `npm run lint`; ✅ `npm test` 38/38 (criteria validation, sort/filter, minimum stay,
  calendar grid, FlightCard); ✅ `npx expo export --platform ios`. On the iPhone 17 Pro simulator (Expo Go): search home and
  airport picker render and load data from the local API. The full flow through results → details → summary was not
  screenshot-verified end to end.
- **Decisions:**
  - Sorting and filtering are client-side over the day's results (at most a few dozen flights per route per day).
  - A return flight must depart at least 120 minutes after the outbound lands (`MIN_STAY_MINUTES`). The UI disables
    earlier returns; the booking API must enforce the same rule in M11.
  - The summary total is indicative only; the server computes the price when the booking is created (M11). The "continue to
    booking" action is added with the booking flow in M13.

## M9 — `feat(auth): implement registration, login and token rotation`
- **Previous commit:** M8 = `117af00`.
- **Content:** ASP.NET Core Identity (`AddIdentityCore`, password policy: 10+ chars with upper case, lower case and a digit;
  lockout after 5 failures for 15 min); HMAC-SHA256 JWT access tokens (15 min; `sub`, `email`, `role`, `jti`); opaque 256-bit
  refresh tokens (30 days) stored as SHA-256 hashes in the new `refresh_tokens` table (migration `AddRefreshTokens`), rotated
  on every use with family-wide revocation on reuse and an atomic claim so concurrent refreshes cannot both win; endpoints
  `POST /api/v1/auth/{register,login,refresh,logout}` and `GET/PUT /api/v1/me`; `Admin` authorization policy; `ICurrentUser`;
  per-IP fixed-window rate limiting on `/auth` (`429 rate_limited` + `Retry-After`); configurable CORS (off by default);
  `promote-admin` CLI command; bearer scheme in OpenAPI; default `code` values for framework-generated 401/403/404/405.
- **Validation:** ✅ `dotnet build` (0 warnings); ✅ `dotnet format --verify-no-changes`; ✅ `dotnet test` 139/139
  (unit: validators, signing-key rules, refresh-token secrets, access-token claims; integration: registration, duplicate
  e-mail, password composition, login, identical failure responses, lockout, rotation, reuse detection, concurrent refresh,
  logout, hash-only storage, 401 problem responses, profile update, token expiry with a dedicated clock, admin promotion,
  rate limiting). A deliberately broken assertion in the concurrency test failed as expected and was reverted. Manually
  against the dev database: migration applied on startup, register → `/me` round trip, `promote-admin` exit codes 0/1.
- **Decisions:**
  - Without `Jwt:SigningKey`, Development/Testing use a random per-process key (warning logged). Every other environment
    refuses to start. No key is committed anywhere.
  - JWT lifetime is validated against the injected `TimeProvider`, so expiry is testable without real waiting.
  - Strict reuse detection: two racing refreshes with one token end the session; the mobile client must refresh single-flight (M10).
- **Housekeeping:** `dotnet format --verify-no-changes` had not been part of earlier checks and failed on existing code:
  `.editorconfig` demanded a `_` prefix for `private static readonly` and `const` fields, which the codebase consistently
  names in PascalCase. Added explicit PascalCase rules for those, and applied the formatter's whitespace/import-order fixes
  to two M6 test files. The format check is part of every milestone from now on.

## M10 — `feat(mobile): add authentication screens and secure token storage`
- **Previous commit:** M9 = `13e5b1b`.
- **Content:** typed auth API client (`src/api/auth.ts`); `expo-secure-store` storage for the refresh token and cached
  profile (Keychain `AFTER_FIRST_UNLOCK_THIS_DEVICE_ONLY`); framework-agnostic session manager (`src/auth/session.ts`) with
  in-memory access token, single-flight refresh, a generation guard so a refresh finishing after sign-out is ignored, and
  offline-tolerant restore; `AuthProvider` (installs the API client's auth hooks, exposes state via `useSyncExternalStore`,
  drops user-scoped queries on sign-out); login and registration modals (react-hook-form + zod rules mirroring the API,
  server field errors mapped onto fields, friendly messages for `invalid_credentials`, `account_locked`, `email_taken`,
  `rate_limited`); signed-in/signed-out profile tab; reusable `FormTextField` and `FormMessage`; `Screen` now avoids the
  keyboard on iOS so footer actions stay visible.
- **Validation:** ✅ `npm run typecheck`; ✅ `npm run lint`; ✅ `npm test` 68/68 (session manager: restore, rotation,
  single-flight, transient vs. rejected refresh, sign-out races; secure storage; schemas and error mapping; login screen);
  ✅ `npx expo-doctor` 21/21; ✅ `npx expo export --platform ios`. Making refreshes non-shared made the single-flight test
  fail, as intended (reverted). A throwaway jest script (not committed; jest-expo stubs `fetch`, so it used a `node:http`
  shim) ran the real session manager against the local API: register → `/me` → restore with rotation → `PUT /me` → reuse
  of the rotated token revokes the family and the client signs out → wrong password gives `invalid_credentials`. On the
  iPhone 17 Pro simulator the signed-out profile tab and the login modal render; typing into the forms on the simulator
  was not automated (no UI driver installed).
- **Decisions:**
  - Only the refresh token and the last profile are persisted. The access token lives in memory, so a cold start always
    refreshes once.
  - A refresh rejected with 400/401 ends the session; network errors, 5xx and 429 keep it (the cached profile keeps the
    UI signed in offline, and the next request retries).
  - The `expo-secure-store` config plugin is not added: it only configures Face ID usage text, which the app doesn't use.
  - `npm run typecheck` now regenerates typed routes first (see `docs/testing.md`), which CI needs too.

## M11 — `feat(bookings): implement booking creation, seats and cancellation`
- **Previous commit:** M10 = `341743d`.
- **Content:** `BookingService` and `/api/v1/bookings` endpoints: create (one-way or return, 1–9 passengers, at least
  one adult, fare snapshot, 20-minute hold) with a required `Idempotency-Key` (request-hash check, `Idempotent-Replayed`
  header, race-safe via the unique index); list/detail; per-segment seat map with server-side seat prices; seat selection
  (replace-per-passenger, unique-index arbitration → `409 seat_unavailable`); cancellation quote and cancel with
  fare-rule refunds (simulated). Capacity uses an atomic conditional `UPDATE` (`FlightCapacity`). `BookingExpiryService`
  lazily expires due holds on the affected flights before writes; reads report lapsed holds as `Expired`.
  `SaveChangesAsync` maps PostgreSQL unique violations to `UniqueConstraintViolationException` with the constraint name.
- **Validation:** ✅ `dotnet build` (0 warnings); ✅ `dotnet format --verify-no-changes`; ✅ `dotnet test` 174/174
  (112 unit, incl. new booking validator and `Idempotency-Key` tests; 62 integration, incl. 13 booking tests: return
  booking with fare snapshot and totals, idempotent replay and key reuse, 8 concurrent retries with one key → one booking,
  10 users racing for the last 3 seats → exactly 3 × 201 and 7 × 409, 6 users racing for one seat → exactly one success,
  seat pricing per category and Flex free seats, IDOR → 404, cancellation of pending and confirmed Plus bookings
  (50% refund), the 24-hour cutoff, and lazy expiry with a dedicated clock); `has-pending-model-changes` reports none.
  Manually against the dev API: register → search → create (201) → replay (200, `Idempotent-Replayed: true`) → seat map
  (A320, 180 seats) → seat 12C (+15.99) → quote → cancel → list.
- **Bug found by the tests:** domain entities generate their own Guid ids, but EF Core's convention treated Guid keys as
  store-generated, so children added to a tracked aggregate (seat holds, payment attempts) were sent as `UPDATE`s and
  failed with concurrency exceptions. All `Entity.Id`s are now `ValueGeneratedNever` (no schema change, no migration).
- **Decisions:**
  - Lazy expiry runs each due booking in its own short transaction before the main one; a concurrency conflict there
    means another request already handled it.
  - The hold (20 min) outlives the access token (15 min); clients refresh transparently, the expiry test signs in again.
  - The payment API is not part of this milestone; tests confirm bookings through the domain model until M12.

## M12 — `feat(payments): implement simulated checkout`
- **Previous commit:** M11 = `9685041`.
- **Content:** `POST /api/v1/bookings/{reference}/payments` with a required `Idempotency-Key` (request hash, replay with
  `Idempotent-Replayed`, `422` on key reuse) and a per-user rate limit. `PaymentService` runs a two-phase flow: it saves a
  `Pending` attempt (bumping the booking version via `LastPaymentStartedAt`), charges `IPaymentGateway` outside any
  transaction, then records the outcome. `SimulatedPaymentGateway` accepts only test tokens (`sim_success`,
  `sim_declined`, `sim_insufficient_funds`; anything else → `invalid_payment_token`); no card data exists anywhere.
  A pending attempt younger than `Booking.PaymentTimeout` (2 min) keeps the hold alive and blocks seat changes and
  cancellation (`409 payment_in_progress`); older ones are failed as `payment_timeout`. Recurring jobs
  (`BookingExpiryJob` every minute, `FlightHorizonJob` every 6 h, `BackgroundJobs` settings) sweep lapsed holds and roll
  the flight horizon forward. Migration `AddPaymentIdempotencyAndClaim` adds `payment_attempts.request_hash` and
  `bookings.last_payment_started_at`.
- **Validation:** ✅ `dotnet build` (0 warnings); ✅ `dotnet format --verify-no-changes`; ✅ `dotnet test` 199/199
  (128 unit, incl. payment-in-flight domain rules, token validation, request hash and the simulated gateway; 71
  integration, incl. 7 payment tests: success with seat confirmation and replay/key reuse/second payment/seat change
  rejected, the three decline codes then success, 8 parallel payments with different keys → exactly one charge, 6
  parallel retries with one key → one attempt, payment after the hold lapsed → `409 booking_expired` with capacity
  released, validation/IDOR/anonymous, rate limit → `429`; and 2 job tests: expiry sweep and horizon rollover);
  `has-pending-model-changes` reports none. Manually against the dev API (migration applied on start-up): booking →
  `sim_declined` (201 Failed, booking still PendingPayment) → `sim_success` (201 Succeeded, Confirmed) → replay (200,
  `Idempotent-Replayed: true`, same payment id) → new key (409 `booking_not_payable`); both jobs ran at start-up.
- **Decisions:**
  - Declines are `201` with `status: Failed`: the attempt was created and is a business outcome, not a client error.
  - The charge is not cancellable once started (`CancellationToken.None`), so a disconnecting client cannot leave
    money taken without a recorded outcome.
  - Jobs are disabled in tests and invoked through `RunOnceAsync`, so they never race with assertions.

## M13 — `feat(mobile): implement booking and checkout flow`
- **Previous commit:** M12 = `2032dd3`.
- **What it adds:**
  - **Trip summary:** a Continue button that leads to sign-in if needed.
  - **Passengers screen:** React Hook Form with Zod and a field array. Adults are listed first and the account holder is
    pre-filled. It creates the booking with an `Idempotency-Key`.
  - **Seats screen:** segment and passenger chips, plus an accessible seat map with category prices, taken seats and
    the booking's passengers' initials. Each tap saves one passenger's seat, then moves to the next passenger without
    one. Conflicts are explained and the map is reloaded.
  - **Checkout screen:** a server-priced breakdown and a choice of simulated payment outcomes, with a notice that no
    real payment is taken and no card details are collected.
  - **Confirmation screen:** the booking reference and a summary. It clears the booking draft.
  - **Shared pieces:**
    - `PendingBookingGate` shows each step only while the booking awaits payment.
    - `HoldBanner` shows a live countdown that reloads the booking when the hold lapses.
    - `createIdempotencyKeys` keeps one key per request body until a definite outcome.
  - **New dependency:** `expo-crypto` (`randomUUID`), installed with `npx expo install`.
- **Validation:**
  - ✅ `npm run typecheck`, ✅ `npm run lint`, ✅ `npx expo-doctor` (21/21), ✅ `npx expo export --platform ios`.
  - ✅ `npm test`: 97/97, of which 29 are new.
    - Unit tests cover the idempotency keys, the passenger schema and request building, seat-map layout and next
      passenger, the hold countdown, and error classification and messages.
    - Component tests cover `SeatMapView` (accessibility labels, taken seats can't be pressed, initials).
    - Checkout screen tests cover:
      - Paying with the chosen method.
      - A decline followed by success with a new key.
      - A lost response retried with the same key.
      - An expired hold shown instead of the pay button.
  - On the iOS simulator (Expo Go), the new routes load from a fresh Metro bundle, and the passengers screen shows its
    signed-out state.
  - ⚠️ Not done: tapping through the whole flow on the simulator. I can't drive the simulator from here. The API side of
    the same sequence was checked by hand in M12, and E2E tests are planned for M20.
- **Notes:**
  - Metro started with `CI=1` does not watch files. New route files only appear after Metro restarts.
  - Test `QueryClient`s use `gcTime: Infinity`. Otherwise cache timers keep Jest running after the tests finish.
- **Decisions:**
  - Seats are saved per tap, not batched at the end. The server is the source of truth for who holds a seat, and a
    conflict shows up straight away rather than at checkout.
  - The create-booking key lives in the draft context. Going back and pressing Continue again replays the same booking
    instead of creating (and holding seats for) a second one.

## M14 — `feat(mobile): add trips, booking details, cancellation and profile`
- **Previous commit:** M13 = `44f358d`.
- **What it adds:**
  - **Trips tab:** an infinite, pull-to-refresh list of the user's bookings. Each card shows a status badge (Awaiting
    payment, Confirmed, Completed, Cancelled or Expired). The tab also has signed-out, empty, error and offline states.
  - **Booking details (`trips/[reference]`):**
    - Each segment's flight, fare conditions and seats; the passengers; the total; and the simulated refund for
      cancelled bookings.
    - A "Continue to payment" button for unpaid bookings while the hold is running.
    - A two-step cancellation based on the server's quote (refund amount, or the reason the booking can't be
      cancelled).
  - **Profile:** name editing (`PUT /api/v1/me`, Zod-validated). The session manager's stored profile is updated so
    new bookings pre-fill the new name. The profile also has a short note on what data is stored.
  - **API client:** `bookingsApi.list`, `cancellationQuote` and `cancel`, plus the hooks `useMyBookings`,
    `useCancellationQuote` and `useCancelBooking`.
- **Validation:**
  - ✅ `npm run typecheck`, ✅ `npm run lint`, ✅ `npx expo-doctor` (21/21), ✅ `npx expo export --platform ios`.
  - ✅ `npm test`: 109/109, of which 12 are new:
    - Status labels.
    - Trips: signed out, list and navigation, empty, retry after an error.
    - Booking details: cancellation with a refund, refused by the quote, rejected by the server, and continuing an
      unpaid booking.
    - Profile: update with session sync, and local validation.
  - The Trips tab renders on the iOS simulator (signed-out state).
  - ⚠️ Not done: tapping through signed-in flows on the simulator. I can't drive the simulator from here. The list,
    quote and cancel endpoints were checked by hand against the dev API in M11.
- **Decisions:**
  - Bookings are shown as one list, newest first, with status badges. They are not split into upcoming and past tabs,
    because sections built on the client from paged data would move items around as more pages load.
  - Unpaid bookings can be cancelled too; the domain allows it, and it releases the held seats straight away.

## M15 — `feat(admin): implement flight management capabilities`
- **Previous commit:** M14 = `fd23dd1`.
- **What it adds:** the `/api/v1/admin` API, which requires the `Admin` role.
  - **Airports:** list, create, update, activate and deactivate. Airports are never deleted.
  - **Schedules:** list and create. Creating a schedule generates its flights for the horizon straight away.
  - **Flights:** list and filter with load and fares, reprice or close a fare, and cancel a flight. Cancelling a flight
    cancels and fully refunds all of its active bookings.
  - **Bookings:** search, a detail view with history and payment attempts, and cancellation on behalf of the airline
    (full refund, ignoring the 24-hour cutoff).
  - **Payments:** `PaymentService` refuses payment for a booking whose flight is cancelled (`409 flight_cancelled`).
  - **Refactoring:** `FlightDataImporter.GenerateFlightsAsync` can be limited to one schedule, and `BookingService`
    shares its booking query and DTO mapping with the admin services.
- **Validation:**
  - ✅ `dotnet build` (0 warnings), ✅ `dotnet format --verify-no-changes`.
  - ✅ `dotnet ef migrations has-pending-model-changes`: none. M15 needs no schema change.
  - ✅ `dotnet test`: 206/206, of which 7 are new admin integration tests:
    - Role enforcement (401 and 403).
    - Airport lifecycle and search visibility.
    - Schedule creation: generated flights, overlap, unknown aircraft, wrong airline prefix, and validation.
    - Fare repricing and closing: existing bookings keep their price, and new bookings get the new price or are refused.
    - Flight cancellation: confirmed and unpaid bookings are cancelled, the refund is full even on Basic, capacity is
      released, and a repeat cancels nothing.
    - The payment guard for a cancelled flight.
    - Admin booking search and cancellation inside the customer's 24-hour cutoff.
  - ⚠️ Not done: exercising the new `.http` admin requests by hand against the dev API. The integration tests cover the
    same endpoints through the real HTTP pipeline and database.
- **Decisions:**
  - The flight's status is committed before its bookings are processed, and each booking is cancelled in its own
    transaction. A booking with a payment in flight is reported as skipped rather than blocking the whole operation, and
    repeating the call finishes it.
  - Schedules are create-only. Changing a timetable that has sold flights needs re-accommodation, which is out of scope.
  - Known edge case: two schedules for the same flight number created at the same instant could both pass the overlap
    check. The flights' unique index (number + local date) still prevents duplicate flights.

## M16 — `build(docker): containerize API and add production-like compose stack`
- **Previous commit:** M15 = `1a7f6f1`.
- **What it adds:**
  - **`backend/Dockerfile`:** multi-stage, with a non-root `aspnet:10.0` runtime on port 8080. A separate
    `migrations` target holds an EF Core migration bundle for the release step. `backend/.dockerignore` keeps
    `bin`/`obj`, tests and `.env` files out of the build context.
  - **`docker-compose.yml`:** a new `api` profile. A one-shot `migrate` service runs before the API, which starts in
    the Production environment. The plain `docker compose up -d` still starts only Postgres.
  - **`.env.example`:** `JWT_SIGNING_KEY` (a placeholder), `API_PORT` and `OPENAPI_ENABLED`.
  - **Forwarded headers:** opt-in support (`ForwardedHeaders:Enabled`) so per-IP rate limiting works behind a TLS
    proxy.
- **Validation:**
  - ✅ `dotnet build` (0 warnings), ✅ `dotnet format --verify-no-changes`, ✅ `dotnet test` 206/206.
  - ✅ `docker build` of both targets on Colima (linux/arm64). Inside the image, `/usr/share/zoneinfo` and ICU are
    present and the process runs as `uid=1654(app)`.
  - ✅ `docker-compose --profile api up`:
    - `migrate` exited 0 ("database is already up to date"), against the existing dev database, without resetting it.
    - `/health/live` and `/health/ready` returned 200.
    - The API worked end to end: a flight search returned 3 offers, registration returned 201, and Swagger returned
      200.
    - Logs are compact JSON.
    - The containers were removed afterwards; Postgres and its data were left untouched.
  - ⚠️ This machine has no buildx, so images were built with Docker's deprecated legacy builder through the standalone
    `docker-compose` v2. The Dockerfile uses no BuildKit-only features, so it builds with either builder.
  - ⚠️ Not tested: forwarded-header handling has no automated test. It is opt-in and off by default.
  - ⚠️ Not done: an amd64 image build. CI builds natively on amd64 (M17).
- **Decisions:**
  - The migration bundle uses `aspnet` rather than `runtime` as its base image, because Infrastructure depends on the
    ASP.NET Core shared framework through Identity. With `runtime` it exited with code 150.
  - Compose falls back to a clearly named local-only signing key, so the stack starts without a `.env`. Real
    environments must set `Jwt__SigningKey`; Production refuses to start without one.

## M17 — `ci: add GitHub Actions workflow`
- **Previous commit:** M16 = `cfec75a`.
- **What it adds:**
  - **`.github/workflows/ci.yml`:** backend (Release build, format, pending-model check, all tests), Docker (both image
    targets) and mobile (`npm ci`, typecheck, lint, Jest, expo-doctor, iOS and Android export). The token is read-only
    and older runs on the same branch are cancelled.
  - **`.github/dependabot.yml`:** weekly updates for NuGet, Actions and Docker. npm is excluded because Expo packages
    must be upgraded together.
- **Validation:**
  - ✅ `actionlint` 1.7.7 (official Docker image, with shellcheck): 0 errors in both files.
  - ✅ Every workflow command was run locally exactly as written:
    - Release build with 0 warnings; format; no pending model changes; `dotnet test --no-build -c Release` 206/206.
    - `docker build` of both targets (M16).
    - typecheck, lint, `test:ci` 109/109, expo-doctor 21/21, and `expo export` for iOS and Android.
  - ⚠️ Not run: the workflow itself has never run on GitHub, because nothing has been pushed (that needs the owner's
    permission). For the same reason there is no CI badge in the README. Add one only after a green run.
- **Decisions:**
  - CI uses the latest .NET 10 SDK (`10.0.x`) rather than pinning the exact patch. `global.json` allows newer feature
    bands, and the newer patch matches the 10.0.x package versions.
  - Docker images are built, never pushed. Publishing to a registry needs credentials and a decision from the owner
    (see `docs/deployment.md`).

## M18 — `build(mobile): add EAS build profiles with placeholder identifiers`
- **Previous commit:** M17 = `d64f001`.
- **What it adds:**
  - **`eas.json`:**
    - `development` (dev client, iOS simulator), `development-device`, `preview` (internal distribution, Android
      APK) and `production` (store builds, auto-incremented build numbers, `appVersionSource: remote`).
    - The `EXPO_PUBLIC_API_URL` values are placeholders: `https://api-staging.example.com` and
      `https://api.example.com`.
    - The `submit.production` block uses placeholders `<APP_STORE_CONNECT_APP_ID>` and `<APPLE_TEAM_ID>`. Google Play
      uses the `internal` track, as a draft.
  - **`app.config.ts`:**
    - Layered over `app.json`. Identifiers come from `APP_IOS_BUNDLE_ID` and `APP_ANDROID_PACKAGE`, falling back to
      the placeholder `com.example.skyjourney`.
    - Non-production variants get a `.development` or `.preview` suffix and a labelled name, so they install next to
      the store build.
    - `EAS_PROJECT_ID` and `EXPO_OWNER` are read when set.
  - **`expo-dev-client` 57.0.19:** installed with `npx expo install`; published 2026-09-11.
  - **`.gitignore`:** `/secrets/` keeps the Play service-account key out of Git.
- **Validation:**
  - ✅ `npx expo config` resolves the development variant (`com.example.skyjourney.development`) and the production
    variant with an overridden bundle id.
  - ✅ typecheck, ✅ lint, ✅ Jest 109/109, ✅ expo-doctor 21/21, ✅ `expo export` for iOS and Android.
  - ⚠️ Not done: no EAS build or submit has been run. That needs an Expo account (`eas login` / `eas init`), an Apple
    Developer membership and a Google Play Console account. None of these credentials were available, and none were
    invented.
- **Decisions:**
  - Account-specific values are environment variables or explicit `<PLACEHOLDER>`s, never guessed values. The
    `example.com` identifiers cannot be submitted to either store, by design.
  - `usesNonExemptEncryption: false` is set because the app only uses OS-provided TLS. The owner must confirm this
    export-compliance answer for their own submission.

## M19 — `docs: add deployment guide`
- **Previous commit:** M18 = `efb0e8b`.
- **What it adds:** `docs/deployment.md`.
  - What gets deployed, and a comparison of three hosting options. Azure Container Apps with managed PostgreSQL is the
    recommended default. No prices are quoted; check current pricing.
  - The full environment-variable reference.
  - The release procedure: build, push, migrate with the bundle, roll out, and health probes.
  - Provisioning the first administrator.
  - The EAS build and submit sequence (TestFlight first, then Play internal).
  - A credentials checklist for the owner.
- **Validation:**
  - ✅ `promote-admin` runs through the API image against the compose database. With a non-existent email it exits 1
    with "No account with e-mail …"; no data was changed.
  - ✅ Every existing migration's `Up` is additive, as the guide says; drops appear only in `Down`.
  - ⚠️ Not done: none of the cloud or store steps were run. They need the owner's accounts and credentials.

## M20 — `test(e2e): add end-to-end journey tests and API smoke script`
- **Previous commit:** M19 = `014ad04`.
- **What it adds:**
  - **`EndToEnd/JourneyTests`** (2 tests), which use the public API only:
    - A round trip for 2 adults: search, sign-up, booking at exactly the quoted price, seat selection with seat
      fees, a declined then a successful simulated payment, the trip list, the Flex cancellation quote, and
      cancellation with a full refund. Seat inventory is back where it started afterwards.
    - An admin flight cancellation that refunds a paid Basic booking in full and removes the flight from sale.
  - **`scripts/e2e_smoke.py`**, a black-box smoke test over real HTTP against any running API. It uses only the Python
    standard library. It checks health, search, register, booking with an idempotent replay, payment, details and
    list, the quote, cancellation, and that bookings require authentication.
  - **`docs/testing.md`**: an end-to-end section.
- **Validation:**
  - ✅ `dotnet format --verify-no-changes`.
  - ✅ `dotnet test`: 208/208 (2 new).
  - ✅ The smoke script passed against the dev API (`http://localhost:5080`). It booked SJ102 BUD-LHR Flex at 190.99
    EUR, then confirmed and cancelled it with a full refund. It was not run against the compose container in this
    milestone, because the compose stack itself was verified in M16.
- **Deviation from the plan:** the plan listed Maestro flows. Maestro and the Java it needs are not installed, and
  installing system software was not part of the agreed scope. Flows that were never run would be misleading
  placeholders, so none were written. Mobile UI E2E remains a known gap; see `docs/testing.md`.

## Fix — `fix(mobile): keep Expo Go as the default development target`
- **Previous commit:** M20 = `aff07b2`.
- **Found while writing the README (M21):** M18 installed `expo-dev-client`. Once that package is present, `expo start`
  targets a custom development build instead of Expo Go. None exists yet (it needs `eas build` or a local native
  build), so `npm run ios` would have stopped opening the app in Expo Go.
- **Change:** `start`, `ios` and `android` now pass `--go`. A new `start:dev-client` script serves an installed
  development build.
- **Validation:**
  - ✅ `expo start --go` starts Metro.
  - ✅ The flag forces Expo Go mode. Checked in `@expo/cli`'s `resolveOptions`: `--go` overrides the automatic
    dev-client detection.

## M21 — `docs: add README, learning guide and final verification`
- **Previous commit:** Fix = `549fa24` (after M20 = `aff07b2`).
- **What it adds:**
  - **`README.md`:** features, repository layout, local setup, iPhone instructions, checks, a documentation index, and
    status.
  - **`docs/learning-guide.md`:** the concepts behind the code, with pointers to source files, practice ideas, and
    known limitations.
- **Final verification (this commit's tree):**
  - Backend:
    - ✅ `dotnet build`: 0 warnings, 0 errors.
    - ✅ `dotnet format --verify-no-changes`.
    - ✅ `dotnet test`: 208/208 (unit and integration, including concurrency and end-to-end).
    - ✅ `has-pending-model-changes`: none.
  - Mobile:
    - ✅ typecheck, ✅ lint, ✅ Jest 109/109 (23 suites), ✅ expo-doctor 21/21.
  - ✅ The smoke script passed against the dev API (M20).
  - ✅ The README's `promote-admin` command, run through the launch profile, exits 1 for an unknown e-mail. No data was
    changed.
  - ⚠️ Not run: GitHub Actions, EAS builds, store submissions, cloud deployment. See the README "Status" section.
