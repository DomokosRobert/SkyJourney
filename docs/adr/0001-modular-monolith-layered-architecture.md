# ADR-0001: Modular monolith with layered architecture

## Context
One developer and one product. The domain (search, booking, seats, payments) is tightly coupled by
transactions: a booking must reserve capacity and seats atomically.

## Decision
A single deployable ASP.NET Core API with four projects (Domain, Application, Infrastructure, Api)
and one PostgreSQL database. Features are organised by folder inside each layer (`Flights`, `Bookings`, …).
No microservices, message brokers or GraphQL.

## Consequences
- ACID transactions across booking, seats and payments. One thing to deploy and observe.
- Boundaries are enforced by project references, not by network calls. If a module ever needs to
  scale independently, its folder is already a seam to extract.
