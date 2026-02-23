# Deployment: Single service + Postgres via Docker Compose

## Install Docker on Ubuntu

1. Install Docker Engine (see [Install Docker Engine on Ubuntu](https://docs.docker.com/engine/install/ubuntu/)).
2. Install Docker Compose plugin: `sudo apt-get update && sudo apt-get install docker-compose-plugin`.
3. Ensure your user is in the `docker` group: `sudo usermod -aG docker $USER` (log out and back in).

## Run with Docker Compose

1. Copy env example and set secrets:
   ```bash
   cp .env.example .env
   # Edit .env: set ConnectionStrings__DefaultConnection and POSTGRES_PASSWORD (if using docker-compose).
   ```
2. Build and start (prod-like):
   ```bash
   docker compose up -d --build
   ```
3. App listens on port **8080**. API: `http://<host>:8080/api/*`. SPA: `http://<host>:8080/app/`.
4. Health: `GET http://<host>:8080/api/health`.

## DNS (for reverse proxy)

Point the app subdomain at the server IP with an **A record**:

- **Name:** `catalog` (or `catalog.<yourdomain.com>` depending on your DNS provider).
- **Value:** Public IP of the VPS where Docker runs.

Example: `catalog.example.com` → `203.0.113.10`.

## Reverse proxy (HTTPS → app:8080)

After DNS is set, use Nginx or Caddy in front of the app so HTTPS is terminated at the proxy and traffic is forwarded to the app on port 8080.

### Nginx example

```nginx
server {
    listen 443 ssl;
    server_name catalog.example.com;
    ssl_certificate     /etc/ssl/certs/catalog.example.com.crt;
    ssl_certificate_key /etc/ssl/private/catalog.example.com.key;

    location / {
        proxy_pass http://127.0.0.1:8080;
        proxy_http_version 1.1;
        proxy_set_header Host $host;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
    }
}
```

Run the app on the same host (e.g. `docker compose up -d`), so the proxy can reach `http://127.0.0.1:8080`.

### Caddy example

```caddy
catalog.example.com {
    reverse_proxy 127.0.0.1:8080
}
```

Caddy obtains and renews TLS certificates automatically.

## Required environment variables

- **ConnectionStrings__DefaultConnection** — PostgreSQL connection string (Host, Port, Database, Username, Password).
- **SeedData__SuperAdmin__Email** and **SeedData__SuperAdmin__Password** — Used on first run to seed the SuperAdmin user; password must meet complexity. Override via `SEED_SUPERADMIN_EMAIL` and `SEED_SUPERADMIN_PASSWORD` in `.env` when using docker-compose.
- **ASPNETCORE_ENVIRONMENT** — Set to `Production` in production.
- **ASPNETCORE_URLS** — Set to `http://+:8080` when running in Docker so the app listens on 8080.

Do not hardcode secrets; use `.env` (and keep it out of version control) or your platform’s secret store.
