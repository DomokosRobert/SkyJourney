# Testing

| Suite | Location | What it covers | Needs |
|-------|----------|----------------|-------|
| Unit | `backend/tests/SkyJourney.UnitTests` | Domain rules (money, DST, schedules, booking state machine, refunds), validators, pricing, the demo data set | .NET 10 SDK |
| Integration | `backend/tests/SkyJourney.IntegrationTests` | The real API hosted in memory (`WebApplicationFactory`) against a throwaway PostgreSQL 17 container (Testcontainers) | .NET 10 SDK + a running Docker engine |
| Mobile | `mobile/SkyJourney.Mobile` | Type checking, lint, component/unit tests (jest-expo) | Node 22 |

## Running

```bash
cd backend
dotnet test                                           # everything
dotnet test --project tests/SkyJourney.UnitTests      # unit tests only, no Docker
dotnet test --project tests/SkyJourney.IntegrationTests
```

Both test projects use **xUnit v3 on Microsoft Testing Platform**. `backend/global.json` opts the
`dotnet test` command into MTP, which the .NET 10 SDK requires for xUnit v3.

### Integration-test conventions
- One PostgreSQL container per test run (an xUnit *assembly fixture*: `SkyJourneyApiFactory`). Migrations
  and demo seeding run exactly as they do in development.
- The clock is a `FakeTimeProvider` frozen at **2026-11-02 06:00 UTC**, and the seed horizon is 21 days, so
  flights, prices and sales-cutoff behaviour are identical on every run and every machine.
- Tests that change data (sold-out flights, cancellations) use their own route and date, so they don't
  interfere with other tests that run in parallel.
- Auth tests register a unique `user-<guid>@example.com` per test. Tests that move time forward (token expiry)
  or need different settings (rate limits) start a second in-memory host on the same database with its own
  `FakeTimeProvider` (`factory.WithOwnClock(...)`), so the shared frozen clock never changes.
- The shared host raises the auth rate limit, because every in-memory request comes from the same "client IP".

### Docker via Colima (macOS)
Testcontainers starts a helper container (Ryuk) that mounts the Docker socket. With Colima, the
host-side socket path can't be mounted into the VM, and the error is
`invalid mount config for type "bind": stat …/.colima/docker.sock: operation not supported`.
Point Testcontainers at the socket path *inside* the VM:

```bash
export TESTCONTAINERS_DOCKER_SOCKET_OVERRIDE=/var/run/docker.sock
```

Docker Desktop and Linux CI runners don't need this.
