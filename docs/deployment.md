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

## Production environment variables

Production `.env` must not be committed.

Required variables for the current Docker Compose setup:

```txt
POSTGRES_PASSWORD=<strong database password>
SEED_SUPERADMIN_EMAIL=<initial super admin email>
SEED_SUPERADMIN_PASSWORD=<strong initial super admin password>
APP_DOMAIN=catalog.rhda.us
```

The app receives these values through Docker Compose:

```txt
ASPNETCORE_ENVIRONMENT=Production
ASPNETCORE_URLS=http://+:8080
ConnectionStrings__DefaultConnection=Host=postgres;Port=5432;Database=redhead_sites_catalog;Username=postgres;Password=${POSTGRES_PASSWORD}
SeedData__SuperAdmin__Email=${SEED_SUPERADMIN_EMAIL}
SeedData__SuperAdmin__Password=${SEED_SUPERADMIN_PASSWORD}
```

Security rules:

* Never commit `.env`.
* Never commit production passwords or database dumps.
* Do not reuse weak seed passwords.
* After first successful production setup, rotate or remove temporary bootstrap credentials if the application flow allows it.

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
 docker compose up -d --build

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

git pull

docker compose build app

docker compose up -d

docker compose ps

curl -I https://catalog.rhda.us/api/health
```

If Docker Compose needs to rebuild everything:

```bash
docker compose up -d --build
```

Do not run destructive Docker commands such as `docker compose down -v` unless you intentionally want to delete persistent volumes.

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
docker compose build app
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
