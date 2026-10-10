# SkyJourney

A mobile-first flight-booking app built as a learning and portfolio project: a .NET 10 REST API with PostgreSQL, and an
Expo (React Native) app for iPhone first and Android second.

> **Demo data only.** Every airline, flight and fare is generated deterministically by the app itself. Nothing in
> SkyJourney books a real flight, and payments are simulated with test tokens. No card number, CVV or bank detail is
> ever requested or stored.

## Features

| Area | What it does |
|------|--------------|
| Search | Airport search, one-way and return trips, price calendar, results with sorting and filters, flight details with Basic / Plus / Flex fares |
| Booking | Passenger details, server-side pricing with fare snapshots, a 20-minute hold, seat maps with paid and included seats |
| Checkout | Simulated payment (`sim_success`, `sim_declined`, `sim_insufficient_funds`), idempotent retries, confirmation |
| Trips | Booking history, details, a cancellation quote and rule-based refunds (Basic 0 %, Plus 50 %, Flex 100 %; full refund when the airline cancels) |
| Account | Registration, login, rotating refresh tokens with reuse detection, profile and sign-out |
| Admin API | Airports, schedules (which generate flights), fares, flight cancellation with refunds, booking oversight. Use it through Swagger or `SkyJourney.Api.http`. |
| States | Loading, empty, error and offline states on every screen. Offline banner. Hold countdown. |

Safety under concurrency: an atomic capacity counter, a partial unique index on seat reservations, optimistic locking
on bookings, and idempotency keys on bookings and payments. See [ADR-0005](docs/adr/0005-capacity-and-seat-concurrency.md)
and [ADR-0008](docs/adr/0008-simulated-payments-and-idempotency.md).

## Repository layout

```
backend/                 .NET 10 solution (Api, Application, Domain, Infrastructure) and unit/integration tests
mobile/SkyJourney.Mobile Expo SDK 57 app (Expo Router, TanStack Query, React Hook Form + Zod, SecureStore)
docs/                    plan, architecture, ADRs, testing, deployment, implementation log, learning guide
scripts/e2e_smoke.py     black-box smoke test against a running API
docker-compose.yml       PostgreSQL for development, plus an optional containerised API stack (profile "api")
.github/workflows/ci.yml backend, Docker and mobile checks
```

## Prerequisites

- .NET SDK 10.0.1xx (`backend/global.json`)
- Node.js 22 and npm
- Docker (Docker Desktop, or Colima on macOS; see [testing.md](docs/testing.md#docker-via-colima-macos))
- For iOS: Xcode with an iOS simulator, or the **Expo Go** app on a physical iPhone

## Run it locally

```bash
# 1. Database (PostgreSQL 17 on host port 5433)
cp .env.example .env                 # optional: change the local password
docker compose up -d postgres        # or: docker-compose up -d postgres

# 2. API on http://localhost:5080. It applies migrations and seeds demo flights on startup in Development.
cd backend
dotnet run --project src/SkyJourney.Api --launch-profile http
#    Swagger UI: http://localhost:5080/swagger    Health: http://localhost:5080/health/ready

# 3. Mobile app (in a second terminal)
cd mobile/SkyJourney.Mobile
npm ci
npm run ios                          # opens the iOS simulator in Expo Go
```

### On a physical iPhone
1. Install **Expo Go** from the App Store. Put the iPhone on the same Wi-Fi network as your Mac.
2. Start the API as above. It listens on `0.0.0.0:5080`, so other devices on the LAN can reach it. Allow incoming
   connections if the macOS firewall asks.
3. In `mobile/SkyJourney.Mobile`, run `npm start` and scan the QR code with the iPhone camera.
4. The app reaches the API at your Mac's LAN IP on port 5080, worked out from the Metro address. To use another API,
   set `EXPO_PUBLIC_API_URL`, e.g. `EXPO_PUBLIC_API_URL=https://my-api.example npm start`.

On Android, `npm run android` uses the emulator. The emulator reaches the host's API through `10.0.2.2:5080`.

### Trying it out
- Search **BUD → LHR** (or any route from the airport picker) for a date in the next 90 days.
- Register any e-mail address. No e-mail is sent.
- At checkout, choose the simulated outcome: success, declined or insufficient funds.
- To get an admin account, register in the app, then run
  `dotnet run --project src/SkyJourney.Api -- promote-admin you@example.com` from `backend` and sign in again.

### Containerised API (production-like)
```bash
docker compose --profile api up --build   # runs the migrations bundle, then the API on http://localhost:8080
```

## Checks

```bash
cd backend
dotnet build && dotnet format --verify-no-changes
dotnet test                                    # 208 tests; integration tests need Docker (Testcontainers)

cd mobile/SkyJourney.Mobile
npm run typecheck && npm run lint && npm test && npx expo-doctor

python3 scripts/e2e_smoke.py [BASE_URL]        # creates a test user and booking: dev/staging only
```

On macOS with Colima, set `TESTCONTAINERS_DOCKER_SOCKET_OVERRIDE=/var/run/docker.sock` first.

## Documentation

| Document | Contents |
|----------|----------|
| [Implementation plan](docs/implementation-plan.md) | Scope, phases, milestones |
| [Architecture](docs/architecture.md) | Layers, data model, API, security, operations |
| [ADRs](docs/adr) | The nine key decisions and their alternatives |
| [Testing](docs/testing.md) | Test suites, conventions, CI |
| [Deployment](docs/deployment.md) | Hosting options, configuration, release and EAS steps (prepared, not executed) |
| [Implementation log](docs/implementation-log.md) | Every milestone with its commit and validation results |
| [Learning guide](docs/learning-guide.md) | A guided tour of the concepts, with pointers into the code |

## Status

- Not deployed anywhere. No app has been built with EAS, signed, or submitted to a store. The deployment and store
  steps need the owner's accounts; see [deployment.md](docs/deployment.md).
- The CI workflow exists but has not run on GitHub yet, so there is no status badge.
- Known limitations are listed at the end of the [learning guide](docs/learning-guide.md#known-limitations).
