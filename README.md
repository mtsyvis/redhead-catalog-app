# Redhead Sites Catalog

Internal web application for Redhead Digital Agency to manage a large catalog of advertising sites.

The app is used to browse, search, filter, import, edit, and export sites data. It also includes user management, role-based access, export limits, and production deployment via Docker Compose.

## Documentation

Use this repository documentation in the following way:

* `README.md` — project overview, setup, commands, and repository structure.
* `AGENTS.md` — short operating instructions for AI coding agents such as Codex and Cursor.
* `docs/business-requirements.md` — current business rules and product behavior.
* `docs/deployment.md` — production deployment, VPS, Docker, Caddy, PostgreSQL, and backup notes.

Old planning files, commit summaries, and legacy specs are historical context only. They should not be treated as current source of truth unless a task explicitly says so.

## Tech stack

Backend:

* ASP.NET Core / .NET 10
* EF Core
* PostgreSQL
* ASP.NET Core Identity with cookie authentication
* Clean Architecture-style project split

Frontend:

* React
* TypeScript
* Vite
* MUI and MUI DataGrid
* Day.js
* Vitest and Testing Library

Deployment:

* Docker Compose
* PostgreSQL container
* ASP.NET Core app container
* Caddy reverse proxy
* The backend serves the built React SPA from `wwwroot` in production.

## Repository structure

```txt
redhead-catalog-app/
├── .cursor/rules/                         # Cursor rules
├── docs/                                  # Product and deployment documentation
├── scripts/                               # Operational helper scripts
├── src/
│   ├── Redhead.SitesCatalog.Api/          # ASP.NET Core API + SPA host
│   ├── Redhead.SitesCatalog.Application/  # Application services and DTOs
│   ├── Redhead.SitesCatalog.Domain/       # Domain entities, constants, interfaces
│   ├── Redhead.SitesCatalog.Infrastructure/ # EF Core, PostgreSQL, persistence
│   └── Redhead.SitesCatalog.Web/          # React + TypeScript frontend
├── test-data/                             # Test/import sample data
├── tests/
│   └── Redhead.SitesCatalog.Tests/        # Backend tests
├── Caddyfile
├── Dockerfile
├── docker-compose.dev.yml
├── docker-compose.yml
├── global.json
└── redhead-catalog-app.sln
```

## Prerequisites

Required:

* .NET SDK matching `global.json`
* Node.js 22+ and npm
* PostgreSQL 16 recommended

Useful tools:

* Docker Desktop or Docker Engine
* `dotnet-ef` for applying migrations manually
* `nvm-windows` if managing several Node.js versions on Windows

## Local development setup

### 1. Start PostgreSQL

The easiest local option is the development compose file:

```bash
docker compose -f docker-compose.dev.yml up -d
```

This starts PostgreSQL on local port `5433` with database `redhead_sites_catalog`.

### 2. Configure backend settings

For local development, the default `appsettings.json` connection string uses:

```txt
Host=localhost;Port=5433;Database=redhead_sites_catalog;Username=postgres;Password=postgres
```

Do not commit real production secrets. Use environment variables or local user secrets for real credentials.

### 3. Apply migrations

From the repository root:

```bash
dotnet ef database update \
  --project src/Redhead.SitesCatalog.Infrastructure \
  --startup-project src/Redhead.SitesCatalog.Api
```

### 4. Run the backend

From the repository root:

```bash
dotnet build redhead-catalog-app.sln
cd src/Redhead.SitesCatalog.Api
dotnet run --launch-profile http
```

Backend URL:

```txt
http://localhost:5000
```

Health endpoint:

```txt
http://localhost:5000/api/health
```

### 5. Run the frontend

Open a second terminal:

```bash
cd src/Redhead.SitesCatalog.Web
npm install
npm run dev
```

Frontend URL:

```txt
http://localhost:5173
```

In development, Vite proxies `/api` requests to the backend.

## Commands

### Backend

```bash
# Build the solution
dotnet build redhead-catalog-app.sln

# Run backend tests
dotnet test redhead-catalog-app.sln

# Apply EF Core migrations
dotnet ef database update \
  --project src/Redhead.SitesCatalog.Infrastructure \
  --startup-project src/Redhead.SitesCatalog.Api

# Clean build artifacts
dotnet clean redhead-catalog-app.sln
```

### Frontend

```bash
cd src/Redhead.SitesCatalog.Web

# Install dependencies
npm install

# Start development server
npm run dev

# Build production frontend
npm run build

# Run linting
npm run lint

# Run frontend tests
npm run test

# Format frontend files
npm run format
```

### Production Docker build

```bash
docker compose build
docker compose up -d
```

Production configuration is expected to come from `.env` / environment variables. Use `.env.example` as a template, but never commit `.env`.

## Environment variables

Production compose uses these important variables:

```txt
POSTGRES_PASSWORD
SEED_SUPERADMIN_EMAIL
SEED_SUPERADMIN_PASSWORD
APP_DOMAIN
```

The app also supports standard ASP.NET Core environment variable binding, for example:

```txt
ConnectionStrings__DefaultConnection
SeedData__SuperAdmin__Email
SeedData__SuperAdmin__Password
ASPNETCORE_ENVIRONMENT
ASPNETCORE_URLS
```

## Quality gates

Before considering a change complete, run the relevant checks.

Backend:

```bash
dotnet build redhead-catalog-app.sln
dotnet test redhead-catalog-app.sln
```

Frontend:

```bash
cd src/Redhead.SitesCatalog.Web
npm run lint
npm run build
npm run test
```

## Important development rules

* Keep business rules in `docs/business-requirements.md`, not in README.
* Keep AI-agent behavior rules in `AGENTS.md`, not in README.
* Keep deployment-specific details in `docs/deployment.md`, not scattered across prompts.
* Do not use old specs or planning notes as current requirements.
* Server-side authorization is the source of truth; frontend hiding is not security.
* Large catalog operations should preserve server-side filtering, paging, sorting, and export behavior.
* Imports must stay validated, predictable, and test-covered.
* Avoid broad refactors mixed with feature work.

## Production notes

Production runs as a single Docker Compose stack:

* `postgres`
* `app`
* `caddy`

The app container listens on port `8080`. Caddy terminates HTTPS and reverse-proxies traffic to the app container.

Persistent volumes are important:

* PostgreSQL data volume
* ASP.NET Core Data Protection keys
* Caddy data/config volumes

See `docs/deployment.md` for operational details and backup expectations.
