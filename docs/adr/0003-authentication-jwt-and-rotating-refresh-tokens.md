# ADR-0003: Identity + JWT access tokens + rotating refresh tokens

## Context
A native mobile client cannot rely on cookies the way a browser does. Options considered:
1. `MapIdentityApi` bearer tokens: opaque, data-protection based, refresh supported but no reuse detection.
2. An external IdP (Auth0, Entra ID, Keycloak): best practice at scale, but adds a paid or self-hosted dependency.
3. ASP.NET Core Identity for users and passwords + self-issued JWTs + rotating refresh tokens.

## Decision
Option 3.
- `UserManager`/`SignInManager` handle password hashing (PBKDF2 via Identity) and lockout.
- Access token: a JWT signed with HMAC-SHA256 (symmetric key from configuration/secret store) with a 15-minute lifetime.
  Claims: `sub`, `email`, `role`, `jti`.
- Refresh token: 256-bit random value, returned once, stored as a SHA-256 hash with a 30-day lifetime.
  Every refresh **rotates** the token. Presenting an already-rotated token revokes the whole token
  family (reuse detection, RFC 6819 / OAuth 2.0 Security BCP).
- The mobile app keeps the refresh token in SecureStore (Keychain/Keystore) and the access token in memory.

## Consequences
- No external dependency, and it is a good learning vehicle. JWT validation is stateless.
- Signing-key rotation is manual. Moving to asymmetric keys or an external IdP is a contained change in Infrastructure/Api.
