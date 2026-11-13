# Deployment

## Purpose

This document describes the production deployment for Redhead Sites Catalog.

It is an operational reference for VPS deployment, Docker Compose, Caddy, PostgreSQL, backups, and basic troubleshooting.

For local development setup, use `README.md`.

## Current production architecture

Production runs as a single Docker Compose stack on one VPS.

Services:

* `postgres` — PostgreSQL database container.
* `app` — ASP.NET Core application container. It serves both the API and the built React SPA.
* `caddy` — HTTPS reverse proxy.

Network flow:

```txt
Internet
  -> Caddy :80/:443
  -> app:8080
  -> postgres:5432
```

The app is served from the root domain. Do not assume the SPA is hosted under `/app`.

Current production domain:

```txt
catalog.rhda.us
```

Health endpoint:

```txt
https://catalog.rhda.us/api/health
```

## Client catalog protection

* The existing startup migration process applies `AddClientCatalogProtection` before serving requests. Take the usual pre-deployment database backup. The migration adds a nullable personal selection override to `AspNetUsers` and the indexed `ClientCatalogRequests` activity table. Existing users inherit the 100-site selection size; existing export policies are preserved.
* Set `ClientCatalog__RequestsPerMinute` in the deployment environment to a positive integer (default 60). Compose passes it to the app. Changes require restarting the app. Search, Multi-search, export preview, Excel and Google Drive export share this threshold per authenticated Client account.
* Set `ClientCatalog__UniqueSitesPerFiveMinutes` to a positive integer (default 2,000) for the short-term distinct-domain budget. Compose passes it to the app; changes require a restart. This guard applies only to Clients that are not marked as trusted, with a budget at least their selection size. Search, Multi-search and both export destinations share the same atomic check/reservation; preview only checks availability.
* Startup applies `AddTrustedClient` in one transaction. It adds the explicit `IsTrustedClient` flag, marks every account whose previous personal selection was above 100 as trusted, clears those obsolete overrides, closes open hourly alerts for accounts currently in the Client role without sending a pending email, and constrains future personal selection overrides to 1–100. Take the usual pre-deployment database backup. Trusted Clients have no selection cap and bypass the five-minute guard, hourly activity alerts and automatic 24-hour activity ban. Enabling trust clears the in-memory counter and closes any open hourly incident; disabling it restores the guard and hourly monitoring with a fresh five-minute counter and starts a fresh automatic-ban enforcement window. Assigning a protected Client role also starts a fresh automatic-ban window. The request-rate limit, export quotas and activity statistics remain active for Trusted Clients.
* There is no fixed catalog pause. Reaching the distinct-domain budget succeeds; a selection exceeding it receives `429` with a computed `Retry-After`. Repeats and smaller selections can still succeed. The UI preserves results and keeps filters usable without polling a catalog status endpoint. The obsolete `ClientCatalog__CooldownMinutes` setting has been removed and can be deleted from existing deployment environments.
* Both request-rate limiting and the distinct-domain counters use memory within the current single app instance. Restart resets these counters. Domain entries expire after five minutes without issuance. Before running multiple replicas, replace counters with coordinated distributed enforcement and coordinate the alert worker. This guard does not depend on asynchronous activity logging. Failed export generation/delivery conservatively retains reservations.
* Startup applies `AddClientCatalogAlertsAndCooldown`, `UseTimeBasedClientCatalogAlertReview` and `RemoveClientCatalogFixedPause`. The resulting schema stores `ClientCatalogAlerts` with one open incident per account. The follow-up migrations remove the obsolete alert re-arm flag and the user's catalog pause end time. Existing review timestamps and incident history are preserved and determine the 60-minute notification cooldown. Take the usual backup before deployment. Incident/review/delivery history is retained separately from the 30-day request log.
* Set `ClientCatalog__AlertUniqueSitesPerHour` (positive integer, default 5,000) and `ClientCatalog__AlertEmails` (comma/semicolon-separated addresses, defaults to `mtsyvis2405@gmail.com,dmitry.s@redheaddigital.agency`). Both appsettings and Compose provide these recipients; Compose uses them when the environment setting is unset or empty, while a non-empty value overrides them. The worker checks hourly unique-site activity for active non-trusted Clients every minute. Open unsent incidents for protected Clients are delivered to the configured recipients. Delivery also requires the existing `Email__Enabled`, SMTP relay and sender settings, and `Frontend__BaseUrl` for the Users link. Closed incidents are not emailed retroactively. Settings changes require restart.
* Email failures leave incidents pending and retry after five minutes (up to 20 due messages per scan). An incident normally sends once; a crash after SMTP accepts a message but before delivery status is saved can cause a duplicate retry. Check API logs for catalog alert processing/email failures. SuperAdmin `Mark as reviewed` records who reviewed and when, and does not change catalog or export limits; new incidents are suppressed for 60 minutes after the latest manual review, then the rolling hourly threshold applies again even if activity stayed high. Enabling trust closes an open hourly incident, cancels its pending delivery and does not create this cooldown if trust is later removed. The hourly alert never disables an account automatically.
* Startup applies `AddClientCatalogAutoBan`, which creates the persisted auto-ban settings and incident tables and adds the account disable reason/reset metadata. Automatic bans initially have `AutoBanEnabled=false` and a threshold of 20,000 unique sites per rolling 24 hours. SuperAdmin can read or update both values through `Admin > Settings > Client protection` or `GET`/`PUT /api/admin/client-catalog-protection`; the threshold must remain a positive integer even while disabled. The UI warns and requires confirmation before enabling automatic bans or lowering the threshold because retained activity from the previous 24 hours is evaluated on the next scan. The worker reads the database setting once per minute, so changes need no restart and add no per-request database lookup.
* When enabled, an independent minute worker auto-disables active Clients that are not marked as trusted at `>=` the configured rolling-24-hour threshold. It stores one open auto-ban incident and sends a separate email to the existing `ClientCatalog__AlertEmails` recipients, with the same five-minute retry behavior. Its scan and error handling are independent from the hourly-alert worker, so a failure in one does not skip the other. An hourly suspicious email and an auto-ban email can both be sent for a protected Client. In Users, the auto-ban notification takes precedence; reviewing it also closes any open hourly incident for that account.
* Current sessions are signed out on their next API request. Password accounts retain the existing reactivation-link/password-reset flow; Google-only accounts can be reactivated immediately. Successful reactivation starts a fresh auto-ban enforcement window but preserves the underlying request/export history for admin reporting.
* Activity records store user ID, UTC time, route, HTTP status and issued search/Multi-search domains. They do not store cookies, passwords, raw search/filter bodies, or IP addresses. Completed exports are combined from the existing exported-domain records when displaying unique-site totals.
* A background job removes catalog request records older than 30 days every six hours. Existing export-record retention is unchanged. SuperAdmin can inspect hour/day/week totals in the selection-limit dialog and Client user-details page.
* Activity logging runs on response completion in a fresh database scope, so it records the final HTTP status after exception handling (including 400/403 rather than a provisional 500). Logging is best effort: database errors produce a warning rather than breaking catalog responses. Records may be absent if the process stops before completion or persistence; statistics are not a billing ledger. Check API logs for `Could not persist Client catalog activity` or cleanup failures and monitor the activity table size. Recording a response means issuing data, not proof a person read it or a disconnected client received every byte.
* Tune the request threshold using legitimate client activity and 429 counts. No new hourly/daily/weekly viewing quota is enforced. Slow collection and collection across accounts remain possible.
* Verify the data guard using a test Client: reach its budget with distinct search results in under five minutes. The final allowed selection succeeds; a new selection exceeding remaining capacity returns `429`, JSON code `ClientCatalogBurstLimited`, `retryAfterSeconds`, and `Retry-After`. A repeated selection or empty result still succeeds. Confirm that rejected requests do not refresh domain timestamps, smaller selections work when they fit, and capacity recovers as domains expire after five minutes. Results stay visible; filters and Retry remain usable, with no status polling. Different sessions of the same account share enforcement; another account is independent. Verify hourly flags and delivery using local/test settings and a controlled inbox, not a production load test.

## Important files

```txt
Dockerfile
Docker Compose file: docker-compose.yml
Caddy config: Caddyfile
Environment template: .env.example
```

`docs/deploy.md` is legacy and should be replaced by this file or archived after migration.

## Docker Compose services

### postgres

Uses PostgreSQL 16 Alpine.

Important behavior:

* Database name: `redhead_sites_catalog`
* User: `postgres`
* Password comes from `POSTGRES_PASSWORD`
* Data is stored in Docker volume `postgres_data`
* Healthcheck uses `pg_isready`
* Restart policy: `unless-stopped`

### app

Builds from the repository `Dockerfile`.

Important behavior:

* Listens on port `8080` inside the Docker network.
* Exposes port `8080` only to other containers, not directly to the public internet.
* Uses `ASPNETCORE_ENVIRONMENT=Production`.
* Uses `ASPNETCORE_URLS=http://+:8080`.
* Connects to PostgreSQL through Docker DNS host `postgres`.
* Persists ASP.NET Core Data Protection keys in Docker volume `dataprotection_keys`.
* Mounts `/etc/redhead/secrets/google-service-account.json` read-only at `/run/secrets/google-service-account.json` for the optional emergency Sites export.
* Healthcheck calls `http://localhost:8080/api/health`.
* Restart policy: `unless-stopped`.

### caddy

Uses `caddy:2-alpine`.

Important behavior:

* Publishes ports `80` and `443`.
* Reads `APP_DOMAIN` from environment.
* Mounts `Caddyfile` as read-only.
* Stores TLS certificates and Caddy state in persistent volumes.
* Reverse-proxies traffic to `app:8080`.
* Restart policy: `unless-stopped`.

Current `Caddyfile` shape:

```caddy
{$APP_DOMAIN} {
    encode gzip
    reverse_proxy app:8080
}
```

## Webmaster offer currencies

Startup applies [AddWebmasterOfferCurrencies](../src/Redhead.SitesCatalog.Infrastructure/Data/Migrations/20261112060923_AddWebmasterOfferCurrencies.cs).
Take a full database backup before deploying. The prepared data initialization is recorded in the
[data change journal](data-migrations.md#initialize-webmaster-quote-currencies).

Set the optional `ExchangeRateApi__ApiKey` in the private deployment environment to enable
ExchangeRate-API requests. Compose forwards it to the app container; recreate the container after changes.
Keep the key out of source, frontend configuration and logs. Without a key, the worker makes no requests.

The worker runs within the single app instance and persists its next attempt before the HTTP request.
Attempts are spaced 24 hours apart, including after failures and restarts. Failures preserve the last
successful rate table. `CurrencyReferenceRefreshStates` contains the last attempt time/outcome and next
eligible attempt; `CurrencyReferenceSnapshots` contains the current table.
The HTTP timeout is 20 seconds. Request logging and redirects are disabled because the provider key
is part of the URL; never log full request URLs, response bodies or provider exception details.

Schema rollback is blocked while any non-USD `SourceCurrency` remains. Preserve and explicitly resolve
that metadata before rollback. Roll back schema and app binaries together; older binaries are incompatible
with the currency write contract and constraints.

Currency conversion, price metadata and editor behavior are defined in
[business requirements](business-requirements.md#webmaster-offers-and-raw-webmaster-prices).

## LinkDetective market prices

Set `LinkDetective__ApiToken` in the private deployment environment to enable market-price
lookups from the admin-only Price comparison view in Webmaster Offers Single search.
Compose passes it only to the app container.
Keep the token out of frontend configuration, source control, and logs. An empty token leaves
the optional integration unavailable without preventing ordinary catalog use.

The normal startup migration workflow creates a separate price-cache table. Only the three
price aggregates and successful retrieval/error timing metadata are persisted. Existing
catalog prices and webmaster offers are not modified. Cache rows are deleted with their site.
Set `LinkDetective__CacheLifetimeDays` in the deployment environment to an integer from 1 to 365
to change the cache lifetime (default: 30 days). Values outside this range prevent application startup.
Compose forwards it to the app container;
recreate the app container after changing it. Application settings → System limits shows
the effective value read-only.
Missing results and snapshots older than that lifetime are still refreshed when an authorized
admin opens or rechecks Price comparison for a domain. Ordinary
Offers and Multi-search do not call the provider. Manual refresh shares a one-minute
per-domain cooldown. Failed refreshes preserve the previous successful snapshot.

Individual provider HTTP calls have a 15-second timeout and no automatic HTTP retries. The single app instance
serializes provider calls with at least one second between request starts, shares provider
cooldowns across domains, and stops after an exhausted quota until the reported reset (or the
next UTC day for a daily quota when no reset time is returned). Other unknown quota periods
and explicitly expired reset timestamps use a five-minute pause. Inventory preview failures
pause the background run without applying Inventory quota/access/rate cooldowns to on-demand
domain lookups. Refreshes share one application-wide lock; reusable cached results
are read without waiting for that lock, and the cache is checked again before a provider call.
Request pacing and refresh locking are in memory; per-domain
cooldowns are also persisted. Before deploying multiple replicas, replace the in-memory
coordination with distributed coordination. Restart resets the in-memory provider cooldown.

### Optional background market-price sync

The worker is disabled by default. To enable it, set `LinkDetectiveSync__Enabled=true` alongside
`LinkDetective__ApiToken` in the private deployment environment and recreate the app container.
Admin and SuperAdmin have read-only monitoring at `Admin` → `LinkDetective Sync`; it exposes
no token and cannot start or force provider requests. `Refresh status` only reads the database.

Compose forwards the following settings. Invalid settings fail startup:

* `LinkDetectiveSync__DailySelectionTimeUtc`: `HH:mm`, default `00:10` UTC (08:10 Asia/Singapore).
* `LinkDetectiveSync__BatchSize`: positive whole number, default 500. The default is a verified
  working batch size, not a confirmed provider maximum.
* `LinkDetectiveSync__OnDemandReserve`: non-negative checks reserved for on-demand use, default 10000.
  Configure it below the provider's quota; a quota at or below the reserve intentionally pauses the worker.
* `LinkDetectiveSync__MaxAttempts`: 1–10, default 3.
* `LinkDetectiveSync__RetryDelayMinutes`: 1–1440, default 5.

The first enabled startup selects missing/stale catalog domains immediately. Later selections
run daily; an existing run is resumed first and can span several quota periods.
Changing the daily selection time updates the persisted future occurrence after restart;
an already active run continues first. All catalog
domains are eligible, including quarantine. Cache freshness uses the same effective lifetime as
on-demand lookup. A successful manual check can satisfy queued work without another paid lookup.

The worker confirms quota with the free preview before each paid batch. Preview and domain
quota pool differences pause processing and are shown in the monitor; do not assume the
Inventory row limit is a domains batch limit. The UI shows the last observed quota and reset
time, not a live balance. Exhausted reserve waits for the provider reset, or rechecks after 15
minutes when no usable reset time is available. An explicitly expired reset is rechecked rather
than treated as a new future daily reset. Access failures wait at least
an hour. Rate limits respect Retry-After; incomplete/transient domain failures have bounded retries.

Run/item progress, retries, and the next selection are persisted. A PostgreSQL advisory lock
allows only one background worker to process at a time. Each paid attempt is saved before
calling the provider: after a crash its charge is uncertain, and retries may spend checks again.
Confirmed prices and confirmed no-price results update the existing shared cache; errors retain
the previous successful snapshot. A malformed individual result or quota field does not discard
other confirmed domains in the same valid JSON response. Catalog price and offer tables are untouched.

Disable the worker by setting `LinkDetectiveSync__Enabled=false` and recreating the app container.
Saved progress and market prices remain; enabling again resumes pending work. Deployment uses
the normal startup migrations. For schema and rollback details, see the
[sync migration](../src/Redhead.SitesCatalog.Infrastructure/Data/Migrations/20261103080717_AddLinkDetectiveBackgroundSync.cs).

## Production environment variables

Production `.env` must not be committed.

Required variables for the current Docker Compose setup:

```txt
POSTGRES_PASSWORD=<strong database password>
SEED_SUPERADMIN_EMAIL=<initial super admin email>
SEED_SUPERADMIN_PASSWORD=<strong initial super admin password>
APP_DOMAIN=catalog.rhda.us
GOOGLE_DRIVE_CLIENT_ID=<Google OAuth client id>
GOOGLE_DRIVE_CLIENT_SECRET=<Google OAuth client secret>
GOOGLE_DRIVE_REDIRECT_URI=https://catalog.rhda.us/api/integrations/google-drive/callback
GOOGLE_DRIVE_APP_NAME=Redhead Catalog
GOOGLE_DRIVE_EXPORT_FOLDER_NAME=Redhead Catalog Exports
GOOGLE_AUTH_ENABLED=true
GOOGLE_AUTH_CLIENT_ID=<Google OAuth client id>
GOOGLE_AUTH_CLIENT_SECRET=<Google OAuth client secret>
EmergencySitesExport__Enabled=false
EmergencySitesExport__ScheduleCron=30 3 * * MON
EmergencySitesExport__GoogleDriveFolderId=<shared-drive-folder-id>
EmergencySitesExport__ServiceAccountJsonPath=/run/secrets/google-service-account.json
EmergencySitesExport__RetentionWeeks=8
EmergencySitesExport__FilePrefix=redhead-sites-full
EmergencySitesExport__UploadTimeoutMinutes=30
ExportedDomainAccessCleanup__Enabled=true
ExportedDomainAccessCleanup__RetentionDays=30
ExportedDomainAccessCleanup__BatchSize=1000
ExportedDomainAccessCleanup__IntervalHours=24
FRONTEND_BASE_URL=https://catalog.rhda.us
Email__Enabled=true
Email__SmtpHost=smtp-relay.gmail.com
Email__SmtpPort=587
Email__FromAddress=noreply@redheaddigital.agency
Email__FromName=Redhead Catalog
Email__SendTimeoutSeconds=10
```

Ahrefs sync is inactive by default and no longer part of the active production workflow. The
current Traffic/DR history is saved through Sites update import. Historical backfill details are
in [Data change history](data-migrations.md#historical-metric-baseline).

`EmergencySitesExport__ScheduleCron` uses standard five-field cron in UTC: `minute hour day-of-month month day-of-week`.
`ExportedDomainAccessCleanup__RetentionDays` must be at least `7` because client weekly unique-domain limits use a rolling 7-day window.

The app receives these values through Docker Compose:

```txt
ASPNETCORE_ENVIRONMENT=Production
ASPNETCORE_URLS=http://+:8080
ConnectionStrings__DefaultConnection=Host=postgres;Port=5432;Database=redhead_sites_catalog;Username=postgres;Password=${POSTGRES_PASSWORD}
SeedData__SuperAdmin__Email=${SEED_SUPERADMIN_EMAIL}
SeedData__SuperAdmin__Password=${SEED_SUPERADMIN_PASSWORD}
GoogleDrive__ClientId=${GOOGLE_DRIVE_CLIENT_ID}
GoogleDrive__ClientSecret=${GOOGLE_DRIVE_CLIENT_SECRET}
GoogleDrive__RedirectUri=${GOOGLE_DRIVE_REDIRECT_URI}
GoogleDrive__AppName=${GOOGLE_DRIVE_APP_NAME}
GoogleDrive__ExportFolderName=${GOOGLE_DRIVE_EXPORT_FOLDER_NAME}
GoogleAuthentication__Enabled=${GOOGLE_AUTH_ENABLED}
GoogleAuthentication__ClientId=${GOOGLE_AUTH_CLIENT_ID}
GoogleAuthentication__ClientSecret=${GOOGLE_AUTH_CLIENT_SECRET}
EmergencySitesExport__Enabled=${EmergencySitesExport__Enabled}
EmergencySitesExport__ScheduleCron=${EmergencySitesExport__ScheduleCron}
EmergencySitesExport__GoogleDriveFolderId=${EmergencySitesExport__GoogleDriveFolderId}
EmergencySitesExport__ServiceAccountJsonPath=${EmergencySitesExport__ServiceAccountJsonPath}
EmergencySitesExport__RetentionWeeks=${EmergencySitesExport__RetentionWeeks}
EmergencySitesExport__FilePrefix=${EmergencySitesExport__FilePrefix}
EmergencySitesExport__UploadTimeoutMinutes=${EmergencySitesExport__UploadTimeoutMinutes}
ExportedDomainAccessCleanup__Enabled=${ExportedDomainAccessCleanup__Enabled}
ExportedDomainAccessCleanup__RetentionDays=${ExportedDomainAccessCleanup__RetentionDays}
ExportedDomainAccessCleanup__BatchSize=${ExportedDomainAccessCleanup__BatchSize}
ExportedDomainAccessCleanup__IntervalHours=${ExportedDomainAccessCleanup__IntervalHours}
Frontend__BaseUrl=${FRONTEND_BASE_URL}
Email__Enabled=${Email__Enabled}
Email__SmtpHost=${Email__SmtpHost}
Email__SmtpPort=${Email__SmtpPort}
Email__FromAddress=${Email__FromAddress}
Email__FromName=${Email__FromName}
Email__SendTimeoutSeconds=${Email__SendTimeoutSeconds}
```

Security rules:

* Never commit `.env`.
* Never commit production passwords or database dumps.
* Invitation email uses Google Workspace SMTP Relay on port 587 with required STARTTLS and no SMTP authentication. Do not add SMTP passwords, Gmail OAuth, service accounts, or App Passwords.
* Before enabling invitation email, confirm that `noreply@redheaddigital.agency` is permitted as a sender and that VPS public IP remains allowlisted in Google Workspace.
* Do not reuse weak seed passwords.
* Google Drive OAuth uses `https://www.googleapis.com/auth/drive.file`; do not configure broad Drive access.
* Google registration/sign-in uses only `openid`, `profile`, and `email`. It must never request Drive scopes or save Google OAuth tokens.
* Add `https://catalog.rhda.us/signin-google` to the OAuth client's authorized redirect URIs. Keep `https://catalog.rhda.us/api/integrations/google-drive/callback` as the separate Drive redirect URI.
* The same OAuth client credentials may be supplied for both integrations, but keep their environment variables and callback URIs separate. Set `GOOGLE_AUTH_ENABLED=false` if sign-in credentials are not configured.
* Caddy is the only externally reachable service. It forwards the original HTTPS scheme so the application can generate the secure Google callback URI; do not publish the app container port directly.
* The emergency Sites export uses a Google service account JSON file for the configured Shared Drive folder. Mount `/etc/redhead/secrets/google-service-account.json` into the app container as `/run/secrets/google-service-account.json:ro` before setting `EmergencySitesExport__Enabled=true`.
* After first successful production setup, rotate or remove temporary bootstrap credentials if the application flow allows it.

Invitation email is synchronous and best-effort after the invitation has been saved. A temporary SMTP
failure does not make `/api/health` unhealthy and does not roll back the account or invitation. The
admin UI reports the failure without exposing the SMTP response and keeps the activation link available.

After deployment, create an invitation for a controlled test inbox and verify the From address,
subject, activation, and reissue behavior. Check application logs to confirm that invitation tokens,
activation URLs, and email bodies are absent.

## First deployment checklist

From the VPS:

```bash
# 1. Clone repository
cd /opt
git clone <repository-url> readhead-catalog
cd /opt/readhead-catalog

# 2. Create .env
cp .env.example .env
nano .env

# 3. Set required production values
# POSTGRES_PASSWORD=...
# SEED_SUPERADMIN_EMAIL=...
# SEED_SUPERADMIN_PASSWORD=...
# APP_DOMAIN=catalog.rhda.us

# 4. Build and start
 docker compose pull postgres caddy
 docker compose build --pull app
 docker compose up -d

# 5. Check containers
 docker compose ps

# 6. Check health
 curl -I https://catalog.rhda.us/api/health
```

Expected health result:

```txt
HTTP/2 200
```

If DNS is not ready yet, test from inside the Docker network:

```bash
docker compose exec app wget -qO- http://localhost:8080/api/health
```

## Routine deployment

Use this flow for normal updates:

```bash
cd /opt/readhead-catalog

git pull --ff-only

docker compose build --pull app

docker compose up -d

docker compose ps

curl -I https://catalog.rhda.us/api/health
```

`--pull` refreshes the Dockerfile's Node.js build image and .NET SDK/runtime images.
`docker compose up -d --build` alone does not guarantee fresh base images.
To also update the PostgreSQL and Caddy images within their existing major versions, take
the usual database backup, then run:

```bash
docker compose pull postgres caddy
docker compose build --pull app
docker compose up -d
```

Updated images cause the affected containers to be recreated, including a brief database
interruption when PostgreSQL changes. Keep PostgreSQL on major version 16 unless performing
a separately planned database upgrade.

Do not run destructive Docker commands such as `docker compose down -v` unless you intentionally want to delete persistent volumes.

## Security maintenance and version checks

The frontend builds inside the Dockerfile's `node:24-alpine` stage. The final application image
contains ASP.NET Core and the built static frontend, with no Node.js runtime. Host Node.js and
.NET installations are not used by this Docker deployment. Updating Node.js on a laptop or VPS
does not update Docker images or the committed npm lockfile.

All application Dockerfile stages upgrade Alpine packages within the base image's existing
Alpine release. The frontend stage also installs npm 11.21.2. When changing that pinned npm
version, verify the clean frontend build and scan the frontend stage as well as the runtime image.
PostgreSQL and Caddy use unmodified upstream images; `apk upgrade` in the app Dockerfile does
not update those separate services. Open upstream-image findings are tracked in
[TD-005](technical-debt.md#td-005-upstream-container-image-security-findings).

To inspect a host Node.js installation on the VPS:

```bash
command -v node
node --version
npm --version
```

If these commands report that Node.js is missing, this application does not require installing it
on the host. If other host applications use Node.js, check their requirements and installation
method before updating it. Use the latest patched release of a supported LTS line.

Inspect the current build image and the running service versions:

```bash
docker run --rm --pull=always node:24-alpine sh -c 'node --version && npm --version'
docker compose exec app dotnet --list-runtimes
docker compose exec postgres postgres --version
docker compose exec caddy caddy version
docker version
docker compose version
docker compose images
```

The temporary Node.js container reports the freshly pulled build image, not a host installation
or proof that the running app was rebuilt. Rebuild with `docker compose build --pull app`, then
recreate with `docker compose up -d` and verify health as described above.

Run the [dependency audits](../README.md#dependency-security-checks) on the checked-out source
before deployment. They cover npm and NuGet packages, including transitive dependencies;
they do not cover the operating-system packages inside images. With Docker Scout installed
and authenticated, scan the actual local images used by the running stack:

```bash
docker scout cves "local://$(docker compose images -q app)"
docker scout cves "local://$(docker compose images -q postgres)"
docker scout cves "local://$(docker compose images -q caddy)"
```

If Scout is unavailable, an alternative is to scan a saved image with Trivy. This example
runs the scanner against only a temporary archive, without mounting the Docker socket:

```bash
audit_dir=$(mktemp -d /tmp/redhead-security.XXXXXX)
docker image save --output "$audit_dir/app.tar" "$(docker compose images -q app)"
docker run --rm --cap-drop=ALL --security-opt=no-new-privileges \
  --mount "type=bind,source=$audit_dir,target=/reports" \
  --mount type=volume,source=redhead-security-trivy-cache,target=/root/.cache/ \
  aquasec/trivy:0.76.2 image --scanners vuln --input /reports/app.tar
```

Repeat for the PostgreSQL and Caddy image IDs. To check build tooling, also build the frontend
stage with `docker build --pull --target frontend -t redhead-catalog:frontend-security-check .`
and scan that tag. Build tools are absent from the final app image but still execute during builds.

Review findings and available fixes at every severity. A successful scan command is not proof
of zero findings. Refresh images and rebuild/recreate the affected services, then scan again.
Some upstream findings may have no released fix; assess applicability and record unresolved
findings in `docs/technical-debt.md` rather than suppressing audit warnings. A scanner finding
alone does not establish that a vulnerable function is reachable in this deployment.

VPS OS packages and the Docker daemon are a separate maintenance layer. For Ubuntu/Debian,
inspect pending updates with `sudo apt update` and `apt list --upgradable`, then apply them
under the normal VPS maintenance process. Host updates do not replace images or running
containers. Reboots and daemon/database restarts can interrupt the application.

## Data migrations

API startup applies pending EF migrations once. Before deploying a release with a data migration,
take a fresh backup and review its purpose and essential restrictions in
[Data change history](data-migrations.md), with technical details in migration code and tests.
After startup, verify API health and the relevant migration record.

## Logs and status

Show current containers:

```bash
docker compose ps
```

Show recent logs for all services:

```bash
docker compose logs --tail=200
```

Follow app logs:

```bash
docker compose logs -f app
```

Follow Caddy logs:

```bash
docker compose logs -f caddy
```

Follow PostgreSQL logs:

```bash
docker compose logs -f postgres
```

Inspect app environment from inside the container:

```bash
docker compose exec app printenv | sort
```

## Database access

Open a PostgreSQL shell inside the database container:

```bash
docker compose exec postgres psql -U postgres -d redhead_sites_catalog
```

Useful read-only checks:

```sql
SELECT COUNT(*) FROM "Sites";
SELECT COUNT(*) FROM "AspNetUsers";
```

Be careful with destructive SQL commands. Always create a backup before manual data cleanup, imports, migrations, or schema changes.

## Backups

Provider-level VPS backups are useful, but they should not be the only database backup strategy.

The current production PostgreSQL backup setup, weekly emergency Sites Excel export, Google Drive storage details, manual checks, and restore guidance are documented in [`backup-restore.md`](backup-restore.md).

Use both:

1. VPS/provider backups for full-server disaster recovery.
2. PostgreSQL logical dumps before risky application or data changes.

Create a manual database backup:

```bash
mkdir -p backups

docker compose exec -T postgres pg_dump \
  -U postgres \
  -d redhead_sites_catalog \
  --format=custom \
  --file=/tmp/redhead_sites_catalog.backup

docker compose cp postgres:/tmp/redhead_sites_catalog.backup ./backups/redhead_sites_catalog_$(date +%Y%m%d_%H%M%S).backup
```

Alternative plain SQL dump:

```bash
mkdir -p backups

docker compose exec -T postgres pg_dump \
  -U postgres \
  -d redhead_sites_catalog \
  > ./backups/redhead_sites_catalog_$(date +%Y%m%d_%H%M%S).sql
```

Store important backups outside the VPS when possible.

### Restore warning

Restoring a database overwrites production data. Do it only after confirming the backup file and target environment.

Typical restore flow for a custom-format backup:

```bash
# Copy backup into the postgres container
docker compose cp ./backups/<backup-file>.backup postgres:/tmp/restore.backup

# Restore manually from inside the container
docker compose exec postgres sh
pg_restore -U postgres -d redhead_sites_catalog --clean --if-exists /tmp/restore.backup
```

Do not restore directly to production without a fresh backup of the current state.

## Persistent volumes

These volumes must be preserved:

```txt
postgres_data
```

Stores PostgreSQL data. Losing this volume means losing the database.

```txt
dataprotection_keys
```

Stores ASP.NET Core Data Protection keys. Losing this volume can invalidate existing auth cookies and other protected payloads.

```txt
caddy_data
caddy_config
```

Stores Caddy state, including TLS certificates and configuration data.

Do not delete these volumes casually.

## DNS and HTTPS

DNS must point the production domain to the VPS public IP.

Expected DNS shape:

```txt
catalog.rhda.us -> VPS public IP
```

Caddy obtains and renews HTTPS certificates automatically when:

* DNS points to the correct server.
* Ports `80` and `443` are open.
* `APP_DOMAIN` is correct.
* The Caddy container is running.

## Common troubleshooting

### 502 Bad Gateway

Likely causes:

* `app` container is stopped or unhealthy.
* App failed during startup.
* Caddy cannot reach `app:8080`.
* Database is not healthy, so app did not start correctly.

Commands:

```bash
docker compose ps
docker compose logs --tail=200 app
docker compose logs --tail=200 caddy
docker compose logs --tail=200 postgres
```

### App exits immediately

Check app logs:

```bash
docker compose logs app --tail=200
```

Known issue from previous deployments:

* Alpine runtime required `krb5-libs` for `libgssapi_krb5.so.2`.
* The current Dockerfile installs `icu-libs` and `krb5-libs`.

### HTTPS redirection warning

If logs show `Failed to determine the https port for redirect`, remember that HTTPS is terminated by Caddy and the app receives HTTP inside the Docker network.

Do not blindly expose app HTTPS inside the container unless there is a clear deployment reason.

### New build not reflected in production

Use:

```bash
git rev-parse --short HEAD
docker compose build --pull app
docker compose up -d
docker compose ps
```

Then verify health and UI in browser.

If still stale, inspect images and containers:

```bash
docker compose images
docker compose logs --tail=100 app
```

### Cannot connect to database

Check:

```bash
docker compose ps postgres
docker compose logs --tail=100 postgres
```

Verify the app connection string uses Docker host `postgres`, not `localhost`, in production.

## What not to change casually

Do not casually change:

* database name `redhead_sites_catalog`;
* PostgreSQL volume name;
* `POSTGRES_PASSWORD` without a migration/rotation plan;
* `ConnectionStrings__DefaultConnection` host from `postgres` to `localhost` in production;
* app internal port `8080`;
* Caddy reverse proxy target `app:8080`;
* SPA root hosting behavior;
* Data Protection keys volume;
* Caddy volumes;
* Docker restart policies.

Any change in these areas should be intentional, tested, and reflected in this document.

## After deployment

Minimum verification:

```bash
docker compose ps
curl -I https://catalog.rhda.us/api/health
```

Then verify in the browser:

* login page opens;
* login works;
* sites page loads;
* main API calls return successful responses;
* export/import/admin pages work if the release touched those areas.

## Documentation maintenance

Update this file when deployment behavior changes, including:

* Docker Compose services;
* ports;
* environment variables;
* volumes;
* Caddy configuration;
* backup/restore process;
* healthcheck behavior;
* production domain or routing assumptions.
