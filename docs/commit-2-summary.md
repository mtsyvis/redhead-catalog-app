# Commit 2: EF Core + PostgreSQL + Migrations - Implementation Summary

## Status: ✅ COMPLETE

All acceptance criteria have been met:
- ✅ migrations apply cleanly
- ✅ RoleSettings seeded with default values
- ✅ All indexes created as per spec

## What Was Implemented

### 1. Domain Entities (`src/Redhead.SitesCatalog.Domain/Entities/`)

#### Site.cs
- `Domain` (string, PK, required) - Normalized domain (Primary Key)
- `DR` (int) - Domain Rating
- `Traffic` (long) - Traffic count
- `Location` (string, required) - Geographic location
- `PriceUsd` (decimal, precision 18,2) - Base price
- `PriceCasino` (decimal?, nullable) - Casino niche price
- `PriceCrypto` (decimal?, nullable) - Crypto niche price
- `PriceLinkInsert` (decimal?, nullable) - Link insert price
- `Niche` (string?, nullable) - Site niche
- `Categories` (string?, nullable) - Categories
- `IsQuarantined` (bool) - Quarantine status
- `QuarantineReason` (string?, nullable) - Reason for quarantine
- `QuarantineUpdatedAtUtc` (DateTime?, nullable) - When quarantine was updated
- `CreatedAtUtc` (DateTime) - Creation timestamp
- `UpdatedAtUtc` (DateTime) - Last update timestamp

#### RoleSettings.cs
- `RoleName` (string, PK) - Role identifier
- `ExportLimitRows` (int) - Export row limit

#### ExportLog.cs
- `Id` (Guid) - Primary key
- `UserId` (string) - User identifier
- `UserEmail` (string) - User email
- `Role` (string) - User role
- `TimestampUtc` (DateTime) - Export timestamp
- `RowsReturned` (int) - Number of rows exported
- `FilterSummaryJson` (string?, jsonb) - Filter details

#### ImportLog.cs
- `Id` (Guid) - Primary key
- `UserId` (string) - User identifier
- `UserEmail` (string) - User email
- `Type` (string) - Import type ("Sites" or "Quarantine")
- `TimestampUtc` (DateTime) - Import timestamp
- `Inserted` (int) - Rows inserted
- `Duplicates` (int) - Duplicate rows skipped
- `Matched` (int) - Rows matched (quarantine import)
- `Unmatched` (int) - Rows not found (quarantine import)
- `ErrorsCount` (int) - Number of errors

### 2. Entity Configurations (`src/Redhead.SitesCatalog.Infrastructure/Data/Configurations/`)

All entities configured using **Fluent API** (best practice):

#### SiteConfiguration.cs
- Primary key on `Domain` (natural key, varchar(255))
- Index on `Location`
- Index on `IsQuarantined`
- Indexes on `DR`, `Traffic`, `PriceUsd` (for performance)
- Max lengths: Domain (255), Location (100), Niche (200), Categories (500), QuarantineReason (1000)
- Decimal precision: 18,2 for all price fields

#### RoleSettingsConfiguration.cs
- Primary key on `RoleName`
- **Seed data:**
  - Admin: 1,000,000 rows
  - UserManager: 0 rows (export disabled)
  - Editor: 10,000 rows
  - Viewer: 5,000 rows
  - Client: 1,000 rows

#### ExportLogConfiguration.cs
- Index on `TimestampUtc`
- `FilterSummaryJson` stored as `jsonb` (PostgreSQL native JSON)

#### ImportLogConfiguration.cs
- Index on `TimestampUtc`

### 3. DbContext (`src/Redhead.SitesCatalog.Infrastructure/Data/ApplicationDbContext.cs`)

- Inherits from `DbContext`
- DbSets for all entities
- Auto-applies all configurations via `ApplyConfigurationsFromAssembly`

### 4. Migrations (`src/Redhead.SitesCatalog.Infrastructure/Data/Migrations/`)

**Initial Migration:** `20260212060352_InitialCreate`
- Creates all 4 tables
- Creates all indexes
- Seeds RoleSettings data
- Uses PostgreSQL-specific types:
  - `uuid` for Guids
  - `jsonb` for JSON columns
  - `numeric(18,2)` for decimals
  - `timestamp with time zone` for DateTime

### 5. NuGet Packages Added

**Infrastructure:**
- `Npgsql.EntityFrameworkCore.PostgreSQL` (10.0.0) - PostgreSQL provider
- `Microsoft.EntityFrameworkCore.Design` (10.0.3) - Design-time tools

**API:**
- `Microsoft.EntityFrameworkCore.Design` (10.0.3) - For migrations CLI

### 6. Configuration

**Connection String** (`appsettings.json`):
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Database=redhead_sites_catalog;Username=postgres;Password=postgres"
  }
}
```

**Program.cs:**
```csharp
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));
```

### 7. Documentation Updates

**README.md:**
- Added PostgreSQL 15+ to prerequisites
- Added new section: "PostgreSQL Database Setup"
- Instructions for creating database
- Instructions for running migrations

## How to Use

### 1. Install PostgreSQL
Download and install [PostgreSQL 15+](https://www.postgresql.org/download/)

### 2. Create Database
```sql
CREATE DATABASE redhead_sites_catalog;
```

### 3. Update Connection String (if needed)
Edit `src/Redhead.SitesCatalog.Api/appsettings.json`:
```json
"ConnectionStrings": {
  "DefaultConnection": "Host=localhost;Database=redhead_sites_catalog;Username=postgres;Password=YOUR_PASSWORD"
}
```

### 4. Apply Migrations
```bash
# From repository root
dotnet ef database update --project src/Redhead.SitesCatalog.Infrastructure --startup-project src/Redhead.SitesCatalog.Api
```

### 5. Verify Database
Check that tables were created:
```sql
\dt  -- List tables
SELECT * FROM "RoleSettings";  -- Should show 5 seeded roles
```

## Database Schema

```
Sites
├── PK: Domain (varchar(255))
├── IX: Location, IsQuarantined, DR, Traffic, PriceUsd
└── Columns: 18 total (no separate Id column)

RoleSettings
├── PK: RoleName (varchar(50))
└── Columns: 2
    └── Seeded: Admin, UserManager, Editor, Viewer, Client

ExportLogs
├── PK: Id (uuid)
├── IX: TimestampUtc
└── Columns: 7 (includes jsonb field)

ImportLogs
├── PK: Id (uuid)
├── IX: TimestampUtc
└── Columns: 10
```

## Best Practices Followed

1. ✅ **Fluent API Configuration:** All entities configured via IEntityTypeConfiguration
2. ✅ **Separation of Concerns:** Entities in Domain, configurations in Infrastructure
3. ✅ **Nullable Reference Types:** Enabled and properly used
4. ✅ **Indexes:** Created on frequently queried columns
5. ✅ **Seed Data:** RoleSettings seeded via migrations
6. ✅ **PostgreSQL-specific:** Using native types (jsonb, uuid)
7. ✅ **Max Lengths:** All strings have explicit max lengths
8. ✅ **Decimal Precision:** All prices use precision(18,2)
9. ✅ **UTC Timestamps:** All DateTime fields use UTC
10. ✅ **Configuration File:** Connection string externalized
11. ✅ **Natural Primary Key:** Domain as PK (more efficient for lookups, prevents duplicates)

## Troubleshooting

### Migration Command Not Found
Install EF Core tools:
```bash
dotnet tool install --global dotnet-ef
```

### Connection Failed
- Verify PostgreSQL is running: `pg_isready`
- Check connection string credentials
- Ensure database exists

### Permission Denied
Grant permissions:
```sql
GRANT ALL PRIVILEGES ON DATABASE redhead_sites_catalog TO postgres;
```

## Next Steps

As per the plan, the next commit (Commit 3) will add:
- ASP.NET Core Identity integration
- Role-based authorization (RBAC)
- MustChangePassword functionality
- Seed dev admin user

---

**Implementation Date**: February 12, 2026
**Implemented By**: Claude (Cursor AI Agent)
**Spec Reference**: `docs/spec-cursor.txt` (Section 12, Commit 2)
**Database**: PostgreSQL 15+
**ORM**: Entity Framework Core 10.0.3
