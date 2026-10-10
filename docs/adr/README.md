# Architecture Decision Records

Short records of decisions that are expensive to reverse. Format: Context → Decision → Consequences.

| ADR | Title | Status |
|-----|-------|--------|
| [0001](0001-modular-monolith-layered-architecture.md) | Modular monolith with layered architecture | Accepted |
| [0002](0002-rest-minimal-apis-and-url-versioning.md) | REST with minimal APIs and `/api/v1` prefix | Accepted |
| [0003](0003-authentication-jwt-and-rotating-refresh-tokens.md) | Identity + JWT access tokens + rotating refresh tokens | Accepted |
| [0004](0004-data-access-without-repositories.md) | Application layer uses the DbContext directly | Accepted |
| [0005](0005-capacity-and-seat-concurrency.md) | Capacity and seat concurrency control | Accepted |
| [0006](0006-time-representation.md) | UTC instants + IANA time zones + local departure date | Accepted |
| [0007](0007-flight-data-provider-abstraction.md) | Flight-data provider abstraction with deterministic seed | Accepted |
| [0008](0008-simulated-payments-and-idempotency.md) | Simulated payments and idempotency keys | Accepted |
| [0009](0009-mobile-stack.md) | Mobile stack: Expo Router, TanStack Query, SecureStore | Accepted |
