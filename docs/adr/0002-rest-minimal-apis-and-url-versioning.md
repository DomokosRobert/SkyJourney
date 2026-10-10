# ADR-0002: REST with minimal APIs and an `/api/v1` prefix

## Context
Mobile apps cannot be force-updated: old app versions stay in the wild for months, so the API
contract needs a way to evolve. Controllers are not needed for this size of API.

## Decision
- Minimal APIs, one static `Map…Endpoints` class per feature, using `MapGroup("/api/v1/...")`.
- URL-prefix versioning (`/api/v1`) without the `Asp.Versioning` package. A breaking change gets a
  new `/api/v2` group and the old one is kept until telemetry shows no clients use it.
- DTOs only: entities never leave the application layer.
- Errors use RFC 9457 Problem Details with a stable `code` extension (e.g. `seat_unavailable`) that the client switches on.

## Consequences
Simple and explicit. If several versions must coexist with shared handlers, adopt `Asp.Versioning.Http` later.
