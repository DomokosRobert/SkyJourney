# Deployment (prepared, not executed)

> **Status:** nothing in this repository has been deployed, signed, or submitted to a store, and nothing has been
> pushed to a remote. Everything below is prepared and checked locally (see the implementation log for M16–M18). The
> steps need accounts and credentials that only the project owner can create. Some of them cost money, so check
> current pricing before you start.

## 1. What gets deployed

| Piece | Artifact | Built by |
|-------|----------|----------|
| API | `backend/Dockerfile`, final stage (non-root, port 8080) | `docker build backend`, also checked in CI |
| Database migrations | `backend/Dockerfile`, `migrations` target (EF Core bundle) | `docker build --target migrations backend` |
| Database | Managed PostgreSQL 16+ (17 is used locally) | Hosting provider |
| iOS / Android apps | EAS builds (`mobile/SkyJourney.Mobile/eas.json`) | `eas build` |

## 2. Hosting options for the API

All of these run the same container image with a managed PostgreSQL. Pick one; the rest of this guide is
provider-neutral.

| Option | Good fit when | Notes |
|--------|---------------|-------|
| **Azure Container Apps** + Azure Database for PostgreSQL (Flexible Server) | You want the .NET-first path with managed TLS, revisions and scale-to-zero | Run migrations as a Container Apps *job*. Recommended default for this project. |
| **AWS App Runner or ECS Fargate** + Amazon RDS for PostgreSQL | The organisation is already on AWS | Run migrations as an ECS one-off task. Use Secrets Manager for the settings. |
| **Fly.io** or **Render** + their managed Postgres | You want the quickest hobby or portfolio deployment | Simple CLI or Git-based deploys. Check the database backup guarantees of the plan you choose. |

Whichever you choose, you need:
- A container registry. GitHub Container Registry works with the repository's `GITHUB_TOKEN`; the provider's own
  registry also works.
- HTTPS termination at the platform, with an HTTPS URL for the mobile app. iOS App Transport Security blocks plain
  HTTP in release builds.
- Automated database backups with point-in-time restore. Verify that a restore actually works before you go live.

## 3. Configuration (environment variables)

| Variable | Required | Value |
|----------|----------|-------|
| `ConnectionStrings__SkyJourney` | yes | Npgsql connection string, with `SSL Mode=Require` for managed databases |
| `Jwt__SigningKey` | yes | At least 32 random bytes, e.g. `openssl rand -base64 48`. Keep it in the platform's secret store. Changing it signs every user out of their access token; refresh tokens keep working. |
| `ASPNETCORE_ENVIRONMENT` | no | `Production` (the image default) |
| `ForwardedHeaders__Enabled` | behind a proxy | `true` only if the container can be reached **only** through the platform's load balancer (see architecture.md) |
| `FlightData__SeedOnStartup` | no | `true` to import the deterministic demo schedule. This is a portfolio app with demo data only, and seeding is idempotent. |
| `FlightData__HorizonDays` | no | Bookable days ahead, default 90 |
| `OpenApi__Enabled` | no | `false` in production (the default). Enable it temporarily if you need Swagger. |
| `Cors__AllowedOrigins__0` | no | Only for browser clients. The native app does not need CORS. |
| `BackgroundJobs__Enabled` | no | Default `true` (hold expiry and flight horizon). It is safe with several instances. |

Never put these values in `appsettings.json`, the image, or Git. `.env` files are git-ignored; `.env.example` lists the
names only.

## 4. Release procedure (API)

```bash
# 1. Build and tag (CI already proves both targets build)
docker build -t <REGISTRY>/skyjourney-api:<VERSION> backend
docker build --target migrations -t <REGISTRY>/skyjourney-migrations:<VERSION> backend
docker push <REGISTRY>/skyjourney-api:<VERSION>
docker push <REGISTRY>/skyjourney-migrations:<VERSION>

# 2. Apply migrations once, before the new version takes traffic (idempotent, takes EF Core's migration lock)
docker run --rm -e ConnectionStrings__SkyJourney="<CONNECTION_STRING>" <REGISTRY>/skyjourney-migrations:<VERSION>

# 3. Roll out the API image on the platform. Health probes:
#    liveness  GET /health/live   (process up)
#    readiness GET /health/ready  (database reachable)
```

Keep migrations additive (all current ones are). If a future change needs a destructive migration, split it into expand and
contract releases. Never reset a production database to "fix" a migration.

### First administrator
Register the account in the app, then run the promotion command once against the production database:

```bash
docker run --rm -e ConnectionStrings__SkyJourney="<CONNECTION_STRING>" \
  <REGISTRY>/skyjourney-api:<VERSION> promote-admin <ADMIN_EMAIL>
```

The admin has to sign in again before the role is in their token. There is no endpoint or config switch that grants
the role.

### Known log noise
- **Data Protection** warns that its key ring is ephemeral. Nothing uses it today (see architecture.md).
- **Npgsql** prints `Cannot load library libgssapi_krb5.so.2`. Kerberos/GSSAPI authentication is not used, so the
  message is harmless. Install `libgssapi-krb5-2` in the image only if you switch to Kerberos.

## 5. Mobile release (EAS)

Prerequisites, which the owner must create:
- An Expo account.
- An Apple Developer Program membership with an App Store Connect app record.
- A Google Play Console developer account, with the app created and a service account that has release permissions.

```bash
cd mobile/SkyJourney.Mobile
npx eas-cli@latest login
npx eas-cli init                         # writes the EAS project id; or set EAS_PROJECT_ID / EXPO_OWNER
# Choose your identifiers (they cannot be changed after the first store upload):
npx eas-cli env:create --name APP_IOS_BUNDLE_ID --value <YOUR_BUNDLE_ID> --environment production
npx eas-cli env:create --name APP_ANDROID_PACKAGE --value <YOUR_PACKAGE_NAME> --environment production
# Replace the placeholder API URLs in eas.json (preview/production) with your real HTTPS API URL.

npx eas-cli build --profile development --platform ios     # simulator dev client
npx eas-cli build --profile preview --platform all         # internal testers (ad hoc / APK)
npx eas-cli build --profile production --platform ios      # signing credentials are managed by EAS after you sign in to Apple
npx eas-cli submit --profile production --platform ios     # TestFlight, needs <APP_STORE_CONNECT_APP_ID> and <APPLE_TEAM_ID> in eas.json
npx eas-cli build --profile production --platform android
npx eas-cli submit --profile production --platform android # Play internal track as a draft; key at ./secrets/google-play-service-account.json (git-ignored)
```

Order: TestFlight internal testing first, then Google Play internal testing. Both stores need a privacy policy URL and
data-safety or "App Privacy" answers. SkyJourney stores: email, name, passenger names and dates of birth for bookings,
and no payment card data (payments are simulated). The export-compliance answer (`usesNonExemptEncryption: false`) must
be confirmed by the owner.

## 6. Credentials checklist (owner)

| Needed for | Credential | Where it goes |
|------------|------------|---------------|
| API hosting | Cloud account with billing | Provider console |
| Database | Managed PostgreSQL connection string | Platform secret → `ConnectionStrings__SkyJourney` |
| Tokens | JWT signing key | Platform secret → `Jwt__SigningKey` |
| Image registry | Registry login (or GitHub `GITHUB_TOKEN` for GHCR) | CI secret or local `docker login` |
| EAS | Expo account | `eas login` |
| iOS | Apple Developer membership, App Store Connect app id, team id | `eas.json` submit block (placeholders), EAS-managed signing |
| Android | Play Console account and service-account JSON key | `mobile/SkyJourney.Mobile/secrets/` (git-ignored) or EAS secrets |
| Store listings | Privacy policy URL, support contact | Store consoles |
