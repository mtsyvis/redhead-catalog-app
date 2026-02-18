============================================================
12) Step-by-step plan (each step = one commit + reviewable)
============================================================

Commit 1 — Bootstrap + linting
- Create .NET solution:
  - Api, Domain, Application, Infrastructure
- Create React Vite app (TS)
- Add:
  - .editorconfig
  - Backend analyzers + dotnet format config
  - ESLint + Prettier + TS strict
AC:
- dotnet build passes
- npm run lint passes
Commit: chore: bootstrap backend and frontend with linting

Commit 2 — EF Core + Postgres + migrations
- Add Postgres provider + config
- Entities: Site, RoleSettings, ImportLog, ExportLog
- Migrations + seed RoleSettings
AC:
- migrations apply cleanly
Commit: feat: add database models, migrations, and seeds

Commit 3 — Identity + RBAC + MustChangePassword + IsActive
- Identity cookie auth
- Roles: SuperAdmin, Admin, Internal, Client
- Policies per role
- Extend user with MustChangePassword (bool, default true)
- Extend user with IsActive (bool, default true) for soft delete
- Seed dev SuperAdmin user
AC:
- login works; RBAC enforced; IsActive=false blocks login
Commit: feat: identity auth and RBAC policies

Commit 4 — Domain normalization helper + unit tests
- Implement normalization function + tests
AC:
- tests cover scheme/www/path cases
Commit: test: add domain normalization and unit tests

Commit 5 — Sites listing API
- GET /api/sites: server-side paging/sort/filter/search
- Return { items, total }
AC:
- filters correct; performance ok for 60k
Commit: feat: sites listing API with filters and pagination

Commit 6 — Frontend theme + auth + must-change flow
- ThemeProvider (Outfit + tokens)
- BrandButton + PageShell
- Login + protected routes
- /change-password page that blocks other routes while MustChangePassword==true
AC:
- redirects correct; tokens applied globally
Commit: feat: frontend auth scaffold and brand theme

Commit 7 — Sites page (DataGrid)
- Server-side MUI DataGrid
- Filter panel + search input
- Unavailable marker + reason display
AC:
- browse/filter/sort/paginate works end-to-end
Commit: feat: sites grid with server-side filters and search

Commit 8 — CSV export + per-role limits + ExportLog
- Implement /api/export/sites.csv
- Enforce RoleSettings limit; 0 blocks export
- Log ExportLog
- UI: export button
AC:
- limit is enforced
Commit: feat: csv export with per-role limits

Commit 9 — Sites import (ADD-ONLY) + UI + ImportLog
- CSV parsing isolated behind interface (MVP: CSV only; XLSX Future Scope)
- Add-only insert, duplicates/errors report, ImportLog
- UI import page (SuperAdmin/Admin)
AC:
- 60k import works in batches
Commit: feat: sites add-only import with duplicate reporting

## Commit 10A — Multi-search backend (parse + exact match + response)
**Scope**
- Add POST `/api/sites/multi-search`:
  - Parse `queryText` into up to 500 domains (split whitespace/newlines)
  - Normalize inputs
  - Detect duplicates (after normalization) and return `duplicates[]`
  - Exact match on normalized Domain (full string equality)
  - Return:
    - `found[]` as full SiteDto
    - `notFound[]` as list of normalized domains not found in DB
    - `duplicates[]`
- Add unit tests for:
  - normalization + exact match behavior
  - duplicate detection
  - max 500 enforcement
  - whitespace split (spaces/newlines)
**Acceptance criteria**
- Endpoint works for 1 domain and for 500 domains
- Exact match only (no substring)
- Duplicates are removed from search and reported
- `dotnet build` passes
- `dotnet test` passes (if tests exist)

---

## Commit 10B — Multi-search UI (toggle + show Found + Not found UX rules)
**Scope**
- Add Multi-search toggle next to existing search input.
- In Multi-search mode:
  - Use the same search box; switch to multiline input (recommended) for paste.
  - Submit triggers `/api/sites/multi-search` and stores:
    - found rows
    - notFound list
    - duplicates list
- Render in the SAME DataGrid:
  - Found rows first (normal rows)
  - Not found rows appended at end ONLY when no filters active
  - If filters active: hide Not found rows and show hint banner with “Clear filters”
- Duplicates UX:
  - Show a warning banner: “Duplicates removed: X” + “View list” (optional collapse/popover)
**Acceptance criteria**
- Multi-search works end-to-end in UI
- Not found appended only when filters inactive
- When any filter changes from default, Not found hides + hint appears + Clear filters resets filters
- `npm run lint` passes
- `dotnet build` passes

---

## Commit 10C — Multi-search export (filtered Found + always Not found)
**Scope**
- Add POST `/api/export/sites-multi-search.csv`:
  - Accept queryText + filters
  - Apply filters to found
  - Enforce role export limit on found count
  - Append all notFound domains at end of CSV
- UI:
  - Export button uses this endpoint when Multi-search is ON
  - Existing export endpoint remains for normal single/browse mode
**Acceptance criteria**
- Export includes filtered Found rows + all Not found domains appended
- Role export limit applies to Found rows
- Works whether Not found is visible or hidden due to filters
- `dotnet build` passes
- `npm run lint` passes

Commit 11 — Quarantine import + reason + edit dialog
- Quarantine import reads domain + reason and stores it
- Edit dialog supports toggle + optional reason
AC:
- client sees Unavailable; reason stored
Commit: feat: quarantine import and reason support

Commit 12 — Users mgmt + temp passwords + soft delete
- Users page:
  - create user => generate temp password shown once
  - delete user => soft delete (sets IsActive=false)
- SuperAdmin can manage any user including Admins
- Admin can manage non-Admin users
- Reset password restrictions enforced (SuperAdmin can reset any; Admin can reset non-Admins)
AC:
- restrictions enforced server-side; soft delete works
Commit: feat: user provisioning with temp passwords and role restrictions

Commit 13 — Role Settings admin page
- Editable grid for ExportLimitRows
AC:
- changing limits affects export immediately
Commit: feat: role settings admin page

Commit 14 — Production packaging (Option 1A)
- Build SPA into Api wwwroot/app
- SPA fallback routing
- docker-compose templates (app + postgres)
- sample Nginx/Caddy config for HTTPS + domain
AC:
- single-deploy works on VPS
Commit: chore: production packaging and deployment templates
