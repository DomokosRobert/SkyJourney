# ADR-0007: Flight-data provider abstraction with a deterministic seed provider

## Context
SkyJourney must not present invented data as real and must not scrape airlines. Later, a real
schedule/fare API could be integrated.

## Decision
- `IFlightDataProvider` (Application) returns reference data (airports, aircraft) and **schedules
  with fare rules**. `FlightDataImporter` upserts them and generates flight instances for a rolling
  window (default 120 days). The booking domain only ever reads from the local database.
- `DeterministicFlightDataProvider` (Infrastructure) produces fixed airports, a fictional airline
  (`SkyJourney Air`, `SJ`) and schedules. Prices derive from route distance plus a stable hash
  (FNV-1a) of flight number and date, never from `string.GetHashCode` (which is randomised per process).
- Every API response from search includes `"dataSource": "demo"`, and the app shows a demo badge.

## Consequences
- A real provider later implements the same interface (with timeouts, retries and rate limits) and may feed
  live availability. If it fails, the importer stops and logs. It never falls back to fabricated data in production.
- Search/booking code does not change when the provider changes.
