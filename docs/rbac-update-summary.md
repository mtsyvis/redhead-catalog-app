# RBAC Model Update Summary

## Overview
Updated the application to use a simplified RBAC model with 4 roles instead of 5, and added soft-delete support for users.

## New RBAC Roles

### Old Roles (Removed)
- Admin
- UserManager
- Editor
- Viewer
- Client

### New Roles
1. **SuperAdmin** - Full control over everything
   - Can reset passwords for Admins
   - Can remove/disable any account (including Admins)
   - Can manage RoleSettings, run imports, edit sites, export
   - Export limit: 1,000,000 (unlimited)

2. **Admin** - Can manage users and site operations
   - Can manage users (including creating other Admins)
   - Can manage RoleSettings
   - Can run imports (Sites + Quarantine)
   - Can edit sites
   - Can export (subject to RoleSettings limit)
   - Export limit: 1,000,000 (unlimited)

3. **Internal** - Read-only with export
   - Read-only sites access
   - Can export (role limit applies)
   - Export limit: 10,000

4. **Client** - Read-only with export (lower limit)
   - Read-only sites access
   - Can export (role limit applies)
   - Export limit: 5,000

## Soft Delete Feature
- Added requirement for `IsActive` flag on User entity
- "Delete user" in UI will set `IsActive = false` (soft delete)
- When `IsActive = false`:
  - User cannot login
  - User cannot access any API endpoint
- Physical deletion is NOT allowed (audit logs must preserve user references)

## User Entity Fields (To Be Implemented in Commit 3 - Identity)
- `IsActive` (bool, default: true) - soft delete flag
- `MustChangePassword` (bool, default: true for new users) - force password change

## Changes Made

### 1. Documentation Updates

#### docs/spec-cursor.txt
- Updated Section 3 (Roles & Permissions) with new 4-role model
- Added detailed documentation for soft delete (IsActive flag)
- Updated Section 4 (Password provisioning) with new role restrictions
- Added default export limits per role in Section 5.2
- Updated API endpoints to reflect new roles:
  - Imports: SuperAdmin/Admin only
  - Site editing: SuperAdmin/Admin only
  - User management: SuperAdmin/Admin
  - Password reset restrictions documented
- Updated commit plan sections to reference new roles
- Removed obsolete "Open items" about UserManager

#### docs/plan.md
- Updated Commit 3 to include new roles and IsActive field
- Updated Commit 9 (Sites import) to reference SuperAdmin/Admin
- Updated Commit 12 (Users mgmt) to include soft delete functionality and new role restrictions

### 2. Backend Code Updates

#### RoleSettingsConfiguration.cs
- Updated seed data from 5 roles to 4 roles:
  - Removed: UserManager (0), Editor (10000), Viewer (5000)
  - Updated: Admin (1000000), Client (5000)
  - Added: SuperAdmin (1000000), Internal (10000)

#### InitialCreate Migration (Updated)
- Updated the initial migration seed data directly (since app not deployed)
- Now seeds 4 new roles instead of 5 old roles

#### InitialCreate.Designer.cs (Updated)
- Updated HasData to reflect new 4-role model

#### ApplicationDbContextModelSnapshot.cs (Updated)
- Updated RoleSettings HasData to reflect new 4-role model

## Commands to Reset Database

Since the initial migration was updated and you're testing locally, you'll need to recreate the database:

**Option 1: Drop and recreate the database**
```bash
# Drop the existing database
dotnet ef database drop --project src/Redhead.SitesCatalog.Infrastructure --startup-project src/Redhead.SitesCatalog.Api --context ApplicationDbContext --force

# Apply the updated migration
dotnet ef database update --project src/Redhead.SitesCatalog.Infrastructure --startup-project src/Redhead.SitesCatalog.Api --context ApplicationDbContext
```

**Option 2: Manual PostgreSQL drop**
```sql
-- Connect to PostgreSQL and drop the database
DROP DATABASE "RedheadSitesCatalog";  -- or whatever your DB name is
```
Then run:
```bash
dotnet ef database update --project src/Redhead.SitesCatalog.Infrastructure --startup-project src/Redhead.SitesCatalog.Api --context ApplicationDbContext
```

After migration is applied, the RoleSettings table will contain:
- SuperAdmin (ExportLimitRows: 1000000)
- Admin (ExportLimitRows: 1000000)
- Internal (ExportLimitRows: 10000)
- Client (ExportLimitRows: 5000)

## Assumptions Made

1. **Identity Not Yet Implemented**: Since User entity doesn't exist yet (Identity is Commit 3), the `IsActive` and `MustChangePassword` fields are documented but not implemented. They will be added when Identity is implemented.

2. **No Role Constants/Enums**: No role constants or enums were found in the codebase, so only the RoleSettings seed data was updated. When implementing Identity (Commit 3), role constants should be created.

3. **No Breaking Changes for Existing Data**: The migration safely deletes old role rows and inserts new ones. If there are existing users with old roles (UserManager/Editor/Viewer), those references will need to be updated manually or handled in Commit 3.

4. **Export Limits**: 
   - "Unlimited" is represented as 1,000,000 rows
   - 0 means export is disabled
   - Limits are configurable per role via RoleSettings admin page (Commit 13)

5. **API Endpoint Updates**: Documentation updated to reflect new role permissions, but actual authorization policies will be implemented in Commit 3 (Identity + RBAC).

## What Was NOT Changed

- No UI implementation (per requirement)
- No Identity/User entity changes (doesn't exist yet, will be in Commit 3)
- No actual authorization policy implementation (will be in Commit 3)
- No imports, exports, or other feature implementation (per requirement)
- No role constants/enums created yet (will be needed in Commit 3)

## Next Steps (Future Commits)

1. **Commit 3 (Identity)**: 
   - Implement User entity with `IsActive` and `MustChangePassword` fields
   - Create role constants for the 4 new roles
   - Implement authorization policies based on new roles
   - Ensure `IsActive = false` blocks login and API access

2. **Commit 12 (User Management)**:
   - Implement soft delete UI (sets IsActive=false)
   - Implement role restrictions for user creation
   - Implement password reset restrictions (SuperAdmin can reset any; Admin can reset non-Admins)

## Docker Setup for Local Development

**File created:** `docker-compose.dev.yml` (not `docker-compose.yml` to avoid conflict with future production setup in Commit 14)

**PostgreSQL configuration:**
- Running on port **5433** (not 5432 to avoid conflict with Windows PostgreSQL)
- Username: `postgres`
- Password: `postgres`
- Database: `redhead_sites_catalog`

**Commands:**
```bash
# Start PostgreSQL
docker-compose -f docker-compose.dev.yml up -d

# Stop PostgreSQL
docker-compose -f docker-compose.dev.yml down

# Stop and remove volumes (clean slate)
docker-compose -f docker-compose.dev.yml down -v
```

**Connection string updated in `appsettings.json`:**
```
Host=localhost;Port=5433;Database=redhead_sites_catalog;Username=postgres;Password=postgres
```
