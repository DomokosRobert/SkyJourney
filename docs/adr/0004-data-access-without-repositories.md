# ADR-0004: Application layer uses the DbContext directly

## Context
Generic repositories over EF Core usually hide useful features (projections, `AsNoTracking`,
`ExecuteUpdate`, transactions) and duplicate what `DbContext` already provides.

## Decision
Application defines `ISkyJourneyDbContext` (DbSets + `SaveChangesAsync` + transaction access), and
Infrastructure implements it with `SkyJourneyDbContext`. Application services query with LINQ and
project straight to DTOs. Provider-specific operations (e.g. catching PostgreSQL unique violations)
are translated in Infrastructure via small, purpose-specific interfaces only where needed.

## Consequences
- The Application project references `Microsoft.EntityFrameworkCore` (abstractions only, no provider). That is
  an accepted leak in exchange for much less code.
- Unit tests cover the domain. Application services are tested against real PostgreSQL (Testcontainers),
  not in-memory fakes, which also verifies SQL translation and constraints.
