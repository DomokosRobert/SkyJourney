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
