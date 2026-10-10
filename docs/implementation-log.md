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
