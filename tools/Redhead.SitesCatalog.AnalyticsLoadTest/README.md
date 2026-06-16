# Business Demand Analytics Load Benchmark

Opt-in local benchmark for:

```txt
GET /api/admin/analytics/business-demand
```

This tool seeds deterministic synthetic Client export logs and analytics snapshots, logs in as a SuperAdmin, and measures sequential API response time for a one-year date range by default.

It is intentionally not part of the main solution or normal test suite. Do not run it against production.

## Defaults

* Rows: `5,000,000`
* Date range: UTC today minus 364 days through UTC today
* API target: `http://localhost:5000`
* Database: `Host=localhost;Port=5433;Database=redhead_sites_catalog;Username=postgres;Password=postgres`
* Response-time target: `10,000 ms`
* Runs: `3`
* Warmups: `1`

## Run

Start local PostgreSQL, apply migrations, and run the API first.

Seed benchmark rows:

```bash
dotnet run --project tools/Redhead.SitesCatalog.AnalyticsLoadTest -- --seed --rows 5000000
```

Measure the endpoint:

```bash
dotnet run --project tools/Redhead.SitesCatalog.AnalyticsLoadTest -- \
  --measure \
  --email superadmin@example.com \
  --password "your-password"
```

Seed and measure in one command:

```bash
dotnet run --project tools/Redhead.SitesCatalog.AnalyticsLoadTest -- \
  --seed \
  --measure \
  --rows 5000000 \
  --email superadmin@example.com \
  --password 
```


Cleanup benchmark rows:

```bash
dotnet run --project tools/Redhead.SitesCatalog.AnalyticsLoadTest -- --cleanup
```

Cleanup deletes only rows where `ExportLogs.UserId` starts with `analytics-benchmark-client-`.

## Custom Options

```bash
dotnet run --project tools/Redhead.SitesCatalog.AnalyticsLoadTest -- \
  --seed \
  --measure \
  --rows 5000000 \
  --from 2025-06-06 \
  --to 2026-06-06 \
  --target-ms 10000 \
  --base-url http://localhost:5000 \
  --connection-string "Host=localhost;Port=5433;Database=redhead_sites_catalog;Username=postgres;Password=postgres" \
  --email superadmin@example.com \
  --password "your-password"
```

The benchmark exits with code `2` when the slowest measured request exceeds `--target-ms`.
