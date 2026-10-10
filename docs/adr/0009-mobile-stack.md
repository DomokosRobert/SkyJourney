# ADR-0009: Mobile stack — Expo Router, TanStack Query, SecureStore

## Decision
- Expo SDK 57 (latest stable), TypeScript strict mode, and Expo Router for file-based navigation and deep links.
- TanStack Query v5 for server state (caching, retries, offline awareness). React context for the booking draft:
  no Redux or Zustand, because the client state is small.
- React Hook Form + Zod for forms.
- `expo-secure-store` for the refresh token.
- The date picker is a custom calendar component in plain React Native, with no native module. That keeps Android
  compatibility and gives a range-picking UX.
- `jest-expo` + React Native Testing Library for component tests. Maestro flows for E2E (they run on a simulator or device).

## Consequences
Everything runs in Expo Go on a physical iPhone for development. Development builds (EAS) are needed
only once a native module outside Expo Go is added.
