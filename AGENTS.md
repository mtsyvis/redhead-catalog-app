# AGENTS.md

## Purpose

This file gives AI coding agents practical instructions for working in this repository.

It is not the full product specification. Keep it short and operational. Business rules belong in `docs/business-requirements.md`.

## Project overview

Redhead Sites Catalog is a private web application for Redhead Digital Agency.

It manages a large catalog of advertising sites and supports:

* browsing, searching, filtering, and sorting sites;
* importing and updating catalog data;
* exporting filtered site data;
* user management;
* role-based access and export limits;
* production deployment via Docker Compose on a VPS.

## Tech stack

Backend:

* ASP.NET Core / .NET
* EF Core
* PostgreSQL
* ASP.NET Core Identity

Frontend:

* React
* TypeScript
* Vite
* MUI / MUI DataGrid
* Vitest / Testing Library

Deployment:

* Docker Compose
* PostgreSQL container
* ASP.NET Core app container serving API and built React SPA
* Caddy reverse proxy

## Source of truth

Use this priority order:

1. The current task prompt.
2. `docs/business-requirements.md` for product behavior and business rules.
3. `README.md` for setup, commands, repository structure, and general technical context.
4. `docs/deployment.md` for VPS, Docker, Caddy, PostgreSQL, backups, and production operations.
5. Existing code and tests.

Do not treat legacy specs, old planning notes, old commit summaries, or archived documents as current source of truth unless the task explicitly references them.

If requirements conflict, do not guess. Ask for clarification or clearly state the assumption before implementing.

## Working style

Keep changes small, focused, and reviewable.

Before changing code:

1. Read the current task carefully.
2. Read the relevant section of `docs/business-requirements.md`.
3. Inspect existing implementation and nearby tests.
4. Preserve current behavior unless the task explicitly changes it.
5. Prefer existing project patterns over introducing new abstractions.

Avoid broad refactors mixed with feature work. This is a small business app; pragmatic and readable code is preferred over enterprise overengineering.

## Business-critical areas

Be extra careful when touching:

* roles and permissions;
* user creation and password reset flows;
* export limits and export truncation behavior;
* imports and row-level validation;
* domain normalization;
* price fields and service availability statuses;
* multi-search found/not-found behavior;
* `LastPublishedDate` behavior;
* quarantine behavior;
* production Docker/Caddy/PostgreSQL configuration.

Before changing any of these areas, verify the expected behavior in `docs/business-requirements.md`.

## Authorization and security

Server-side authorization is the source of truth.

Frontend hiding is only a UX convenience and must not be used as the only protection.

Do not commit:

* secrets;
* passwords;
* production `.env` files;
* real client data;
* database dumps;
* API keys;
* temporary generated passwords.

Temporary generated passwords must be displayed only once and must not be logged.

Disabled users must not be able to log in or access protected API endpoints.

## Documentation maintenance

Documentation is part of the definition of done when behavior changes.

When a task changes product behavior, permissions, imports, exports, UI rules, deployment, or setup commands, update the relevant documentation in the same change.

Use this mapping:

* Product behavior and business rules -> `docs/business-requirements.md`
* Setup, commands, repository structure, general project overview -> `README.md`
* Production, Docker, VPS, Caddy, PostgreSQL, backups -> `docs/deployment.md`
* AI-agent workflow and repository instructions -> `AGENTS.md`

Do not update documentation for purely internal refactoring unless behavior, commands, or operational expectations changed.

At the end of each task, explicitly state whether documentation was updated. If it was not updated, state why.

## Testing and quality gates

Run relevant checks before considering work complete.

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

If a command cannot be run, explain why and provide the exact command the user should run manually.

## Backend guidance

Follow the existing backend architecture and coding patterns.

Do not introduce new layers, abstractions, packages, or architectural patterns unless the task clearly requires it.

Keep changes understandable, maintainable, and consistent with the surrounding code.

When changing backend behavior, verify authorization, validation, persistence, and tests where relevant.

## Frontend guidance

Follow the existing frontend architecture, component patterns, styling approach, and API usage.

Do not introduce new UI libraries, state-management patterns, routing approaches, or styling systems unless the task clearly requires it.

Keep UI behavior simple, consistent, and suitable for non-technical business users.

When changing user-visible behavior, verify permissions, loading/error states, data formatting, and tests where relevant.

## Testing

Tests are part of the safety net for business behavior.

Do not delete or weaken tests just to make a task pass.

When a task changes observable behavior, update or add relevant tests.

Use the existing test style unless there is a clear reason to change it.

If tests are not added or updated for a behavior change, explain why in the final response.

## Final response format

When finishing a task, report concisely:

1. What changed.
2. Files changed.
3. Commands run and results.
4. Whether documentation was updated.
5. Anything not done or not verified.
6. Any assumptions made.

Do not paste huge diffs into the final response.
