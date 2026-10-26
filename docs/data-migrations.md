# Data migration history

Technical records for historical backfills and data repairs. Read only for tasks involving these
operations; current product behavior belongs in [business requirements](business-requirements.md).
The [deployment guide](deployment.md) owns the general rollout and backup procedure.

Keep each record focused on scope, implementation, verification evidence, and rollback. Link to
source rather than copying SQL or complete parser dictionaries. Record production application
only when confirmed. Never include dumps, credentials, raw contacts, or real client records.

## Webmaster primary contacts

### Application and audit contract

Normal API startup applies pending EF migrations through `MigrateAsync`. The repair SQL is
embedded in the backend assembly; rebuild and deploy the application with its migrations.
Local audit artifacts, temporary checkouts, and dumps are excluded from the Docker build context.

These repairs lock `Webmasters`, `SiteWebmasterOffers`, and `EntityChangeHistories` in
`SHARE ROW EXCLUSIVE` mode: writes are serialized while reads remain available. Schema changes,
data updates, and audit inserts commit in the migration transaction. Failure stops startup and
rolls back the transaction. Take a fresh backup before rollout; reverting the image does not undo
database changes.

Each repair creates one authoritative `Webmaster` history record and a `SiteWebmasterOffer`
record for every linked offer. `Source` is `Data migration`; `ChangedBy` is `Migration:<MigrationId>`.
`ChangesJson` contains old/new `Primary email` plus `Cleanup rule` metadata. The existing History
view renders the rule as a separate reason, excluding it from the changed-field count.
Raw Contact, normalized Contact, offer links, fingerprints, offers, prices, and Sites are preserved.

### 20261012153852_NormalizeWebmasterPrimaryEmails

[Migration source](../src/Redhead.SitesCatalog.Infrastructure/Data/Migrations/20261012153852_NormalizeWebmasterPrimaryEmails.cs).
Application evidence: present in the production dump taken on 2026-10-13 at 11:56:40 UTC.

The original email-only cleanup repairs repeated copies of one email, unambiguous reply/main
markers, and conflicting marked-email selections. It requires the saved value to agree with the
legacy extractor and skips available manual Contact/Primary email edits. Unresolved records remain.
This applied migration and its frozen SQL must not be rewritten to implement later rules.

Local rehearsal on the 2026-10-12 dump verified 766 repaired Webmasters and 830 offer history
entries: 38 repeated-email repairs, 536 missing values selected by explicit markers, and 192
marked-address corrections. Production counts can differ after portal edits.

### 20261014102750_PopulateWebmasterPrimaryContacts

[Migration source](../src/Redhead.SitesCatalog.Infrastructure/Data/Migrations/20261014102750_PopulateWebmasterPrimaryContacts.cs).
Verification: applied locally on 2026-10-14 to a fresh restore of the 2026-10-13 11:56:40 UTC dump;
production application has not been confirmed.

One schema/data migration replaces the undeployed follow-up work. It changes `PrimaryEmail`
from `varchar(320)` to PostgreSQL `text` and replaces its B-tree index with a hash equality index,
allowing contact instructions without oversized primary-contact index entries.

Only NULL/blank values are populated using the current contact resolver. Existing non-empty
values are untouched. Missing values are filled even if previously cleared manually, reflecting
the requirement that every offer have a primary contact. The public field/label remains
Primary Email; renaming it and creating Webmaster Profiles are separate future work.

The audit added reply abbreviations/misspellings such as `answ`, `ans`, `otvet`, `answear`,
`amswer`, and `jndtn`, conversation/new-contact notes, and explicit sender notes. The later
business decision also accepted price notes and `новый`. The complete vocabulary and binding
rules are in the [resolver](../src/Redhead.SitesCatalog.Application/Services/WebmasterOffers/WebmasterPrimaryEmailRepairParser.cs)
and frozen migration SQL. Parenthetical markers can bind to the preceding email; a standalone
marker can bind to the immediately preceding bare-email line, without bridging blank lines.
New imports use the same resolver. Manual submissions require an explicit non-empty primary contact.

Local verification:

| Selection rule | Webmasters |
| --- | ---: |
| Explicit main/reply/contact marker | 1,098 |
| First email in Contact | 2,573 |
| Contact text without email | 135 |
| Unique placeholder for empty Contact | 1 |
| Total | 3,807 |

Verified 6,447 linked offer history entries and zero Webmasters/offers without a primary contact.
All 3,807 SQL results matched the application resolver. Full-row preservation hashes matched
for offers, prices, Sites, and Webmaster fields excluding the changed value/audit timestamp.
Isolated PostgreSQL tests cover selection, non-empty-value preservation, replay idempotence,
long contacts, guarded rollback/reapplication, and atomic schema/data/audit rollback on failure.

### Verification after rollout

Set `migration_id` to the relevant record above in psql:

```sql
\set migration_id '20261014102750_PopulateWebmasterPrimaryContacts'

SELECT "MigrationId" FROM "__EFMigrationsHistory"
WHERE "MigrationId" = :'migration_id';

SELECT "EntityType", COUNT(*)
FROM "EntityChangeHistories"
WHERE "Source" = 'Data migration'
  AND "ChangedBy" = 'Migration:' || :'migration_id'
GROUP BY "EntityType";
```

After the population migration, also verify completeness:

```sql
SELECT COUNT(*) AS offers_without_primary_contact
FROM "SiteWebmasterOffers" o
JOIN "Webmasters" w ON w."Id" = o."WebmasterId"
WHERE nullif(btrim(w."PrimaryEmail"), '') IS NULL;
```

Expected repair counts refer to the named dumps, not an unchanged production database.
Inspect an affected offer's History to verify its source, old/new value, and reason.

### Rollback and tests

Both migrations' `Down` use the authoritative history to restore the original values. Forward
history is retained and rollback entries use `Rollback:<MigrationId>`. Rollback aborts atomically
if an affected Webmaster no longer has the migration's value/timestamp or has a later manual
Contact/Primary email edit. Use a reviewed correction or backup restore when that guard rejects
rollback. The population migration also aborts if any remaining contact exceeds the old
320-character column limit; it restores the old column/index only after restoring values.

PostgreSQL tests create and delete isolated databases; use a disposable local server:

```powershell
$env:REDHEAD_TEST_POSTGRES = 'Host=localhost;Port=5433;Database=postgres;Username=postgres;Password=postgres'
dotnet test redhead-catalog-app.sln --filter FullyQualifiedName~WebmasterPrimaryEmailMigrationTests
```

## 20261026134302_RemoveWhlWebmasterOffers

[Migration source](../src/Redhead.SitesCatalog.Infrastructure/Data/Migrations/20261026134302_RemoveWhlWebmasterOffers.cs).
Production application has not been confirmed. This is a one-time cleanup before an employee
re-imports corrected Webmaster Offers data.

The migration deletes all active and inactive offers whose linked Webmaster's current
`PrimaryEmail` exactly equals the contact-instruction value frozen in the source. Markdown
asterisks are not part of that value. Matching is case-sensitive, without trimming, substring
matching, or matching against raw Contact. Every matching Webmaster is included, even when
several technical Webmasters have the same primary contact.

Existing foreign keys cascade the deletion to raw offer prices and offer/mailbox links.
Webmasters, mailbox definitions, existing change history, Sites, client prices, and other offers
are preserved. No schema or ongoing import rules change. Startup applies the migration once,
in its normal transaction; a failure rolls back deletions and the migration-history entry.

### Preflight and rollout verification

Take a fresh database backup using the [deployment guide](deployment.md#data-migrations).
Pause imports and offer edits for the rollout, and review the affected counts before deploying.
In psql, enter the exact contact value from the migration source:

```sql
\prompt 'Exact Primary Email from migration source: ' primary_contact

WITH targets AS (
    SELECT o."Id", o."WebmasterId"
    FROM "SiteWebmasterOffers" o
    JOIN "Webmasters" w ON w."Id" = o."WebmasterId"
    WHERE w."PrimaryEmail" = :'primary_contact'
)
SELECT COUNT(*) AS offers,
       COUNT(DISTINCT "WebmasterId") AS webmasters,
       (SELECT COUNT(*) FROM "WebmasterOfferPrices" p
        JOIN targets t ON t."Id" = p."SiteWebmasterOfferId") AS prices,
       (SELECT COUNT(*) FROM "SiteWebmasterOfferLinkbuilderMailboxes" m
        JOIN targets t ON t."Id" = m."SiteWebmasterOfferId") AS mailbox_links
FROM targets;

SELECT "MigrationId" FROM "__EFMigrationsHistory"
WHERE "MigrationId" = '20261026134302_RemoveWhlWebmasterOffers';
```

After startup and **before re-import**, verify the migration record exists and rerun the count
query: offers, prices, and mailbox links must be zero. Production counts are not known in advance.

### Re-import and recovery

Use the current UTF-8 CSV import with the exact 30-column header; place contact instructions
in `ContactRawText`. `PrimaryEmail` is derived for new Webmasters, not an import column.
Text without a recognizable email is supported and keeps its case. Reusing the same normalized
Contact reuses the existing Webmaster and preserves its saved primary contact. Changing that
saved primary contact requires manual editing; this cleanup does not reset it.

Deleting an offer removes its unique fingerprint, so even its original source row can be imported
again. Corrected rows create fresh offers/prices; repeated imports then skip exact duplicates.
Domains must already exist in Sites. Review imported, duplicate, unmatched, invalid, and warning
counts and their downloads. A normal restart after re-import must retain the new offers.

`Down` deliberately rejects rollback because deleted data cannot be reconstructed. Reverting
the image does not restore offers. Recover from the pre-rollout backup or re-import reviewed
source data; do not remove the EF migration-history entry to retry the cleanup after re-import.

Local rehearsal on 2026-10-26 verified exact matching, dependent deletion, preservation of
unrelated records, original/corrected CSV re-import, duplicate detection, restart safety,
transaction rollback on failure, and rejected `Down` on isolated PostgreSQL databases.
All 21 existing primary-contact migration and offer-concurrency tests also passed. Solution
build and normal backend tests passed; the generated EF SQL wraps the deletion and
migration-history insert in one transaction. Historical migration tests target their specific
migration rather than later cleanup migrations; offer-concurrency fixtures include the
now-required primary contact.

## Webmaster offer price uniqueness

`20260905070440_EnforceWebmasterOfferConcurrencyAndUniquePrices` enforces one price row per
offer/price type and replaces the old non-unique index. Before deploying it, take a backup and
run this read-only check in the PostgreSQL shell from the deployment guide:

```sql
SELECT "SiteWebmasterOfferId", "PriceType", COUNT(*) AS "RowCount"
FROM "WebmasterOfferPrices"
GROUP BY "SiteWebmasterOfferId", "PriceType"
HAVING COUNT(*) > 1;
```

If rows are returned, postpone deployment and review the full records with the responsible
manager. Different amounts, statuses, or details must not be reconciled automatically by choosing
the newest/cheapest row. Back up records before approved reconciliation and rerun the check.
The migration stops on duplicates; it does not delete or merge prices. Its transaction preserves
the data and index on failure. Deploy the schema and corresponding concurrency-aware app together.

## Historical metric baseline

[20260622083856_BackfillJuneFourthSiteMetrics](../src/Redhead.SitesCatalog.Infrastructure/Data/Migrations/20260622083856_BackfillJuneFourthSiteMetrics.cs)
imports catalog Traffic/DR values into history dated 2026-06-04 with source `AhrefsBaselineImport`.
This historical backfill does not create an Ahrefs sync run. Recurring metric-history behavior
belongs in the business requirements.
