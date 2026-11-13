# Data change history

## Historical metric baseline

**2026-06-22 · [DATA] [SCHEMA]** Metric history needed an initial baseline. Prepared a backfill of catalog Traffic/DR into history dated 2026-06-04. Production application not confirmed. [Migration](../src/Redhead.SitesCatalog.Infrastructure/Data/Migrations/20260622083856_BackfillJuneFourthSiteMetrics.cs).

## Webmaster primary contacts

**2026-10-12 · [DATA]** Legacy contact parsing left missing or incorrect primary emails. A repair normalized unambiguous cases while preserving manual contact edits. Application confirmed in the production snapshot from 2026-10-13. [Migration](../src/Redhead.SitesCatalog.Infrastructure/Data/Migrations/20261012153852_NormalizeWebmasterPrimaryEmails.cs).

**2026-10-14 · [DATA] [SCHEMA]** Some webmasters still had no primary contact, including contacts expressed as instructions rather than emails. Prepared population of missing contacts from Contact while preserving existing values. Locally verified; production application not confirmed. [Migration](../src/Redhead.SitesCatalog.Infrastructure/Data/Migrations/20261014102750_PopulateWebmasterPrimaryContacts.cs).

## Legacy offer cleanup

**2026-10-26 · [DATA]** Legacy WHL offers needed replacement with corrected source data. Prepared deletion of matching offers, prices, and mailbox links to allow re-import. Production application not confirmed; recovery requires a backup or reviewed re-import. [Migration](../src/Redhead.SitesCatalog.Infrastructure/Data/Migrations/20261026134302_RemoveWhlWebmasterOffers.cs).

## Restore missing catalog-price terms

**2026-11-09 · [DATA]** Catalog prices had missing terms despite consistent Active offer terms. Prepared a backfill from matching offers, preserving selling amounts and skipping conflicts. Locally verified; production application not confirmed, and automatic rollback after data changes is unsupported. [Migration](../src/Redhead.SitesCatalog.Infrastructure/Data/Migrations/20261109082829_BackfillCatalogPriceTerms.cs).

## Deactivate existing offers for quarantined sites

**2026-11-11 · [DATA]** Quarantined sites still had Active webmaster offers. Prepared a backfill to mark those offers Inactive and record the correction in history. Locally verified; production application not confirmed, and automatic rollback after data changes is unsupported. [Migration](../src/Redhead.SitesCatalog.Infrastructure/Data/Migrations/20261111051519_DeactivateQuarantinedWebmasterOffers.cs).

## Initialize webmaster quote currencies

**2026-11-12 · [DATA] [SCHEMA]** Existing numeric webmaster prices were confirmed to be operational USD equivalents, with no reliable structured historical quote currencies. Prepared USD metadata initialization that copies amounts exactly, preserves raw details and parent audit fields, and does not reprice offers or change catalog data. The migration has not been deployed to production. [Migration](../src/Redhead.SitesCatalog.Infrastructure/Data/Migrations/20261112060923_AddWebmasterOfferCurrencies.cs).
