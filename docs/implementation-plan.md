# SkyJourney — Implementation Plan

SkyJourney is a learning/portfolio flight-booking app: a .NET 10 REST API with PostgreSQL and an
Expo (React Native) client that targets iOS first and Android second.
**All flight data is generated, demo data.** Nothing in SkyJourney can be booked with a real airline.

## Repository assessment (Phase 0)

| Item | Finding |
|------|---------|
| Contents | Only `README.md` with a single commit (`e1c89dd`). No existing code or conventions. |
| Branches | `main` only, tracking `origin/main` (`git@github.com:DomokosRobert/SkyJourney.git`). |
| Agent instructions | None (`AGENTS.md`, `CLAUDE.md` absent). |
| Git identity | Configured globally (work e-mail). Used as-is. |
| .NET | SDK 10.0.103, runtime 10.0.3. |
| Node / npm | Node 22.22 LTS, npm 10.9. |
| Docker | Docker 29 running. **Host port 5432 is already used by other containers**, so SkyJourney's PostgreSQL publishes on **5433**. |
| iOS tooling | Xcode 26.3 with iPhone 17 simulators. |
| Not installed | `psql`, Maestro (E2E runner), `eas-cli` (it runs through `npx`). |

## Scope

### MVP (in scope)
- Airport search, one-way and return flight search (direct flights), sorting and filtering.
- Fare families (Basic / Plus / Flex) with baggage and cancellation conditions.
- Registration, login, refresh-token rotation, logout, profile.
- Booking creation with passengers, capacity protection and idempotency.
- Seat maps, seat holds and concurrency-safe seat reservation.
- Simulated checkout (test tokens only), confirmation, booking history and details.
- Rule-based cancellation with refund calculation.
- Admin API: airports, schedules (which generate flight instances), flights, fares, bookings.
- Deterministic seed/mock flight-data provider behind a provider abstraction.
- Docker Compose, CI, production Dockerfile, EAS configuration and deployment docs.

### Deferred (documented as future work)
- Connecting itineraries (1+ stops) and multi-city trips. The domain uses `BookingSegment`, so it can grow into these.
- Infant passengers, ancillary products (bags as add-ons), changing a confirmed booking.
- Real payment provider, e-mail, push notifications, external flight-data API.
- Admin UI (the admin interface is the protected API + Swagger UI + `.http` files).

## Milestones

Each milestone ends with format/lint/build/test checks and **one conventional commit**.
Progress and validation results are recorded in [`implementation-log.md`](implementation-log.md).

| # | Phase | Commit | Content |
|---|-------|--------|---------|
| 1 | 0 | `docs: define SkyJourney scope and architecture` | Plan, architecture, ADRs, log |
| 2 | 1 | `chore: establish repository and development tooling` | .gitignore, .editorconfig, Docker Compose (PostgreSQL), env examples |
| 3 | 1 | `feat(backend): create API and application foundation` | Solution, 4 layers, ProblemDetails, logging, correlation IDs, health, OpenAPI |
| 4 | 1–2 | `feat(database): configure PostgreSQL, domain model and initial migration` | Entities, EF configuration, constraints, indexes, first migration |
| 5 | 2 | `feat(flights): implement airport and flight search with seed data` | Provider abstraction, deterministic seed, search endpoints |
| 6 | 2 | `test(flights): add flight search unit and integration tests` | xUnit, Testcontainers, WebApplicationFactory |
| 7 | 1–2 | `feat(mobile): initialize Expo app, design system and navigation` | Expo SDK 57, Expo Router, TanStack Query, theme |
| 8 | 2 | `feat(mobile): implement flight search experience` | Search form, airport picker, calendar, results, details |
| 9 | 3 | `feat(auth): implement registration, login and token rotation` | Identity, JWT, refresh tokens, rate limiting + tests |
| 10 | 3 | `feat(mobile): add authentication screens and secure token storage` | SecureStore, refresh interceptor |
| 11 | 4 | `feat(bookings): implement booking creation, seats and cancellation` | Domain state machine, capacity, holds, idempotency + concurrency tests |
| 12 | 5 | `feat(payments): implement simulated checkout` | Payment attempts, confirmation, expiry job + tests |
| 13 | 4–5 | `feat(mobile): implement passengers, seats, checkout and confirmation` | End-to-end booking flow in the app |
| 14 | 5 | `feat(mobile): implement booking history, details and profile` | Trips, cancellation, profile |
| 15 | 6 | `feat(admin): implement flight management capabilities` | Admin endpoints, admin provisioning command + tests |
| 16 | 8 | `chore(docker): containerize backend` | Multi-stage Dockerfile, compose profile |
| 17 | 8 | `ci: add automated build and test workflow` | GitHub Actions |
| 18 | 8 | `chore(mobile): configure EAS builds and app metadata` | eas.json, app.config.ts, icons |
| 19 | 8 | `docs(deployment): document production deployment` | deployment.md, store checklists, deploy workflow template |
| 20 | 7 | `test(e2e): add critical booking flow coverage` | Maestro flows + API flow test |
| 21 | 9 | `docs: final verification report` | README, learning guide, final report |

The order differs slightly from the template in the brief. The backend for a feature lands first,
then its tests, then the mobile screens that use it, so each commit stays runnable.

## Definition of done (per milestone)
1. Code compiles with warnings treated as errors in the backend (`TreatWarningsAsErrors`).
2. Relevant tests pass (`dotnet test`, `npm test`), and `tsc --noEmit` and `eslint` are clean.
3. `git diff` is reviewed, with no secrets or build artifacts.
4. Docs are updated and the log entry is written.
