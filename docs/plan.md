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
- CSV/XLSX parsing isolated behind interface
- Add-only insert, duplicates/errors report, ImportLog
- UI import page (SuperAdmin/Admin)
AC:
- 60k import works in batches
Commit: feat: sites add-only import with duplicate reporting

Commit 10 — Multi-search
- API + UI modal; cap 500 inputs
AC:
- found/not found correct and fast
Commit: feat: multi-search paste list feature

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