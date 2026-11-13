# Bulk webmaster offer price update — first release

Status: agreed product scope, awaiting implementation. Prepared on 2026-11-12.

Dependency: first merge the currency implementation in its own branch/session. Its current rules
are defined in [business requirements](../business-requirements.md#webmaster-quote-currencies).
Reuse that functionality; do not implement it twice or include the bulk workflow in the currency-only change.

This bulk workflow uses the persisted USD contract and conversion functionality from that
prerequisite. Currency-related clauses below describe integration points, not another FX
implementation. Catalog writes, automatic repricing and tasks remain out of scope.

This document is the implementation brief for a separate coding session. It describes
planned behavior, not functionality already shipped. Read `AGENTS.md` and the relevant
sections of [business requirements](../business-requirements.md) before implementing.
The specific workflow changes below are intentional; unrelated existing behavior stays intact.

## 1. Goal and boundaries

A webmaster sends prices for multiple domains under one placement term. A staff member
updates the corresponding existing offers in one operation, sees exceptions before applying,
and downloads the information an administrator needs to recalculate catalog selling prices.

The first release is a three-step in-app workflow:

1. Enter a price list for one primary contact and one term.
2. Preview matched offers, changes, exceptions, and the catalog-recalculation indicator.
3. Apply eligible changes and open a saved result with downloadable reports.

In scope:

- Bulk editing of existing webmaster offers, including automatic reactivation on numeric input.
- Correcting an offer's missing term from the explicitly selected price-list term.
- Comparing minimum purchase prices across all eligible offers before and after the operation.
- A saved operation result, exception copying/download, and two Excel exports.

Out of scope:

- Tasks, assignments, review queues, task statuses, or task completion controls.
- Any write to Sites, catalog price options, catalog service availability, or quarantine.
- A markup formula, suggested selling prices, automatic repricing, or an in-app catalog editor.
- Creating sites, offers, or webmasters; merging duplicate offers or contacts.
- A new CSV/XLSX update-import workflow for webmaster offers.
- Generating a ready-to-import catalog update CSV, changing existing import semantics,
  Google Drive integration for these new reports, or a new general job framework.

The existing Sites update import remains available outside this workflow. An administrator
may prepare selling prices from the exported purchase data and use that existing import or
edit the catalog manually. Catalog changes must never write back to webmaster offers.

## 2. Existing implementation to build on

Inspect these areas rather than recreating their capabilities:

| Existing capability | Relevant source |
| --- | --- |
| Domain-based Single/Multi-search and current edit entry point | [WebmasterOffers.tsx](../../src/Redhead.SitesCatalog.Web/src/pages/WebmasterOffers.tsx) |
| Offer loading, manual edits, audit and concurrency | [WebmasterOffersService.cs](../../src/Redhead.SitesCatalog.Application/Services/WebmasterOffers/WebmasterOffersService.cs) |
| Versioned edit contract | [WebmasterOfferUpdateModels.cs](../../src/Redhead.SitesCatalog.Application/Models/WebmasterOffers/WebmasterOfferUpdateModels.cs) |
| Amount, availability and term validation | [WebmasterOfferWriteValidator.cs](../../src/Redhead.SitesCatalog.Application/Validation/WebmasterOfferWriteValidator.cs) |
| Current minimum-purchase matching | [PricingService.cs](../../src/Redhead.SitesCatalog.Application/Services/Pricing/PricingService.cs) |
| Catalog-to-offer service mapping | [CatalogPriceMapping.cs](../../src/Redhead.SitesCatalog.Domain/CatalogPriceMapping.cs) |
| Existing Excel generation | [WebmasterOffersExcelExportGenerator.cs](../../src/Redhead.SitesCatalog.Application/Exports/WebmasterOffersExcelExportGenerator.cs), [XlsxWorkbookWriter.cs](../../src/Redhead.SitesCatalog.Application/Services/XlsxWorkbookWriter.cs) |
| Current quarantine-triggered deactivation | [QuarantineWebmasterOffersUpdater.cs](../../src/Redhead.SitesCatalog.Application/Services/QuarantineWebmasterOffersUpdater.cs) |

Important: the manual editor currently submits a complete offer. Its `UpdatePrices` removes
omitted price types and assigns the offer term to submitted price rows. Do not pass a partial
bulk-edit payload to that method and accidentally delete unselected prices or overwrite metadata.
Reuse validation/audit/concurrency patterns, with a focused bulk-update application path.

Historical context: the relevant records in [data migrations](../data-migrations.md) are
"Restore missing catalog-price terms" and "Deactivate existing offers for quarantined sites".
This feature does not rerun either repair, move catalog prices, or change their frozen migrations.

## 3. Access and implementation defaults

Use existing permissions, enforced server-side:

- Opening, previewing, applying and reading saved bulk operations require full offer read
  access and `WebmasterOffersManage`: SuperAdmin, Admin and Editor under the current matrix.
- Downloading new reports also requires `WebmasterOffersExport`: currently SuperAdmin and Admin.
- Authorized bulk editors may copy exception domains, following existing domain-list copy UX.
- Linkbuilder, Internal, Client and Lite cannot use the workflow or its operation endpoints.
- Existing Sites export quotas do not apply. Do not expose data through cached responses or
  guessed operation IDs to an unauthorized user.

These access choices preserve existing edit/export role boundaries; they are implementation
defaults for details not separately discussed. Do not add permissions or widen export access.
Authorized editors can read a shared operation result so another authorized staff member can
continue from its link. Downloads remain separately protected.

## 4. Step 1 — enter the new price list

Add an `Update prices` entry point on Webmaster Offers, visible to authorized bulk editors.
Use a full-width page/stepper suitable for a large pasted table, consistent with existing MUI
tables, search controls and buttons. Do not create another single-offer edit form.

Inputs:

- `Primary email`: one exact current primary contact value. A searchable selector may help
  find it, but the applied scope is an exact match, not a substring search in raw Contact.
  Use the existing primary-contact normalization. Email addresses are case-normalized;
  contact-instruction values retain their existing normalization semantics.
- `Price-list term`: one supported normalized term: No term, Permanent, or positive finite years.
  This is the term declared by the incoming price list, not a strict search filter.
- `Price-list currency`: one common source currency, default USD. Numeric pasted values are
  amounts in this currency. For the first release, split mixed-currency lists into separate
  operations; do not guess currency from symbols or Price Details.
- `Price types to update`: one or more of the ten existing offer price types. Start with Main
  selected; allow changing the selection. Only selected types have input columns.
- A table containing Domain and the selected price columns. Support tab-separated clipboard
  paste from a spreadsheet and corrections in the table, without requiring a file upload.

Keep a fixed, visible column order matching the selected headers; do not guess arbitrary vendor
headers, currencies or column meanings. An optional pasted matching header row can be removed
explicitly. Use existing domain/URL normalization and decimal parsing conventions.

Selected price cells are mandatory for every input domain:

| Input | Main | Other selected price types |
| --- | --- | --- |
| Positive number within existing amount/precision limits | Numeric price, Available | Numeric price, Available |
| YES, case-insensitive | Invalid | AvailableWithUnknownPrice; numeric amount null |
| NO, case-insensitive | Invalid | NotAvailable; numeric amount null |
| Blank, zero, negative, CLEAR or unsupported text | Invalid | Invalid |

There is no clear-value operation or Unknown input in this workflow. Unselected types are
unchanged, subject only to the common-term correction described below. Changing a type's
availability to YES/NO legitimately removes its numeric amount; it is not a blank-cell command.
Preserve price details; this workflow does not parse amounts from or edit free-text details.
For foreign numeric input, compute the persisted USD value using the prerequisite's conversion
policy and retain source/payment metadata for the selected prices. Check the converted
USD value against existing amount limits too. YES/NO has no numeric amount to convert.

Use the existing Webmaster Offers raw-input ceiling (`WebmasterOffersMaxInputs`, currently
50,000) and enforce it on both client and server. Paginate large previews/results; do not render
every row into the DOM. Reuse existing request limits and cancellation patterns where applicable.

`Check changes` performs a preview without mutating offers, webmasters, Sites or edit history.
Changing the contact, term, selected columns or pasted values invalidates the previous preview.
Changing the price-list currency also invalidates it. Use one rate snapshot for preview and
Apply and retain its provenance in the saved result; do not use different rates per row.

## 5. Matching and exception rules

Normalize domains with the existing normalizer. For each domain:

1. The catalog site must exist. Otherwise classify it as Site not found; do not create it.
2. Search offers belonging to any technical Webmaster whose current PrimaryEmail matches
   the selected primary contact. A primary contact is not a unique WebmasterId.
3. Include both Active and Inactive offers and include quarantined sites.
4. For a known price-list term T, eligible offer terms are exactly T or No term.
   When the selected term is No term, eligible offers must themselves have No term.
5. There must be exactly one eligible offer. Never prefer Active, the cheapest, newest,
   exact-term, or one technical Webmaster when several candidates exist.
6. The candidate must satisfy the price-row consistency restriction below and input validation.

| Matching situation | Outcome |
| --- | --- |
| One offer with the selected known term | Update that offer |
| One No term offer and a selected known term | Update it and explicitly correct its common term |
| Selected known term offer plus a No term offer | Multiple matching offers exception |
| Multiple matching offers, even with different statuses | Multiple matching offers exception |
| Contact has offers, but only with another known term | Different term exception |
| Site exists, but this contact has no offer for it | Offer not found exception |
| Another contact has offers for the domain | Those offers do not match; do not relink them |

Malformed structured terms are not No term. Treat relevant malformed offer/price terms as
exceptions instead of repairing or ignoring them to force a unique match. Other well-formed known
terms do not make an otherwise unique T/No term match ambiguous and are not modified.

This workflow assumes one common term per offer. Require all existing price rows on the chosen
offer to have valid structured terms equal to that offer's current normalized term. A mismatch
is an Inconsistent price terms exception for this workflow, not a new global database restriction.

For identical repeated normalized domains with identical proposed values, collapse the repeated
inputs and report the duplicate-input count. Different proposed values for the same normalized
domain are a Conflicting input rows exception; never use last-row-wins.

Exceptions also include invalid domain/amount/availability input and missing selected values.
Keep original row numbers/values and every useful reason. Assign one primary exception category
per domain for non-overlapping category counts; additional details belong to that same domain.

Absence of a catalog selling price, or of the selected catalog term, is not an exception and
must not block an offer update. Neither is loss of the minimum numeric purchase price after
an otherwise valid update.

## 6. Exact write behavior

For each eligible offer:

- Patch only selected price types. Create a missing price row when needed, using existing
  availability rules. Preserve all unselected amounts, availability values and details.
- For foreign numeric input, save the original quote metadata and the converted/rounded USD
  amount together. Preserve metadata of unselected types. A source amount/currency change
  is a real change even if its converted USD amount is identical; it must be visible in history.
- If the selected price-list term is known and the offer has No term, set the offer and all
  its existing price-row structured terms to that known term. Preserve every amount/status
  not explicitly updated. Keep imported `TermRawText` unchanged.
- Show this correction as `No term -> 1 year`, for example, in preview and the saved report.
  It is authorized only for this submitted price list; No term remains distinct from known
  terms everywhere else. Never change one known term into another through this workflow.
- If at least one submitted selected value is numeric, set the offer Active. This applies
  even if its existing amount is identical: reactivation is still a real change.
- If all submitted selected values are YES/NO, preserve the offer's current Active/Inactive status.
- Preserve Id, domain, WebmasterId, PrimaryEmail, Contact, other offer metadata, mailboxes,
  ImportFingerprint and CreatedAtUtc. Do not merge or update Webmaster records.

Do not change site quarantine or other catalog fields. An Inactive offer on a quarantined site
may become Active after numeric input while the site remains quarantined. This is intentional
in this workflow. Do not route these changes through a site-unavailability save that would
immediately deactivate the offer again. Existing quarantine operations keep their current behavior.

An unchanged amount/status can still produce a real change through term correction or activation.
Only a complete business-state no-op leaves offer/price audit timestamps and history unchanged.

## 7. Step 2 — preview, counters and copying exceptions

Show three mutually exclusive domain counters/filters:

`Ready to update: N | No changes: N | Exceptions: N`

- Ready means a valid uniquely matched offer has at least one effective change.
- No changes means valid, uniquely matched input changes no amount, availability, term or status.
- Exceptions means the domain cannot be safely updated by this operation.
- Count domains, not price cells or offers. Surface removed identical duplicate inputs separately.

The preview table repeats Domain as needed for readable price changes, with access to Offer ID,
current/proposed term and status, selected old/new price values, and one advisory boolean:
`Catalog recalculation needed`. Highlight effective changes only.
Keep operational old/new amounts in USD. For foreign input, show its source amount/currency
and conversion context as secondary edit information. For USD, do not repeat a USD equivalent.

Do not add a multi-state Catalog column, catalog actions, selling-price inputs, or
`Current selling price -> Calculated selling price` in this release.

Selecting Exceptions shows Domain, primary reason and explanatory details. Provide:

- Copy all exception domains.
- Filter by primary reason and copy domains in that category.
- Download exceptions for users with export permission (an Exceptions sheet/report is sufficient).

Copy unique normalized domains one per line, in input order. For invalid domain inputs, preserve
the original value and clearly identify it as invalid rather than inventing a normalized domain.
Copied lists must cover the whole filtered result, not just the visible page.

Use `Apply N updates`, where N is the Ready domain count. Clearly state that exceptions will
not be applied. No manual offer-picker or conflict-resolution wizard is required. Staff can copy
exceptions, fix them outside this workflow and start another preview. Disable Apply if N is zero;
exception copying/download must still work without a fake successful update.

## 8. Purchase minima and the advisory catalog indicator

Use keys `(Domain, catalog PriceType, normalized TermKey)`, never one price for a whole domain.
For a key, the minimum purchase price is the lowest positive numeric price whose parent offer
is Active, whose price availability is Available, and whose own structured term exactly matches.
Search all suppliers, not only the selected primary contact. Exclude malformed terms, Inactive
offers, YES/NO/Unknown without an amount, and LinkDetective market references.
Compare persisted `WebmasterPriceUsd` values. Do not recompute existing offers from their
original currencies at today's rates while determining these minima.

Use `CatalogPriceMapping`; enum numeric values differ:

| Catalog type | Offer type |
| --- | --- |
| Main | Main |
| Casino | Casino |
| Crypto | Crypto |
| LinkInsertion | LinkInsertion |
| LinkInsertionCasino | LinkInsertion18Plus |
| Dating | Dating |

Banner, Banner18Plus, HomepageTextLink and HomepageTextLink18Plus remain editable offer types
but have no catalog counterpart and do not independently set the catalog indicator.

For all potentially affected mapped keys, compare before/after minima in consistent operation
snapshots. Include consequences of reactivation and term correction, including unselected price
types whose term moved. A No term -> known-term correction affects both old and new term keys.

- Numeric minimum changed in either direction: indicator true.
- Null -> number or number -> null: indicator true.
- Equal numeric minima, even if the supplying offer changed: indicator false for that key.
- Null -> null: indicator false for that key.
- The domain indicator is true if any affected mapped key changed.

Evaluate keys even when no matching catalog price exists. This indicator means that the purchase
basis changed and catalog work may be needed; it does not prove a particular selling-price change
without the future formula. Site quarantine is informational and does not reject the operation
or silently remove affected domains from its reports.

Example: this supplier's Main price rises 100 -> 120 while another eligible supplier remains 70.
The minimum stays 70, so no catalog-recalculation flag is raised for Main at that term.

Example: the only numeric LinkInsertion price changes 50 -> NO. Updating the offer succeeds;
the minimum changes 50 -> null. This is a catalog-related outcome, not an execution exception.
It does not authorize changing catalog availability to NO.

## 9. Apply, concurrency, history and repeat requests

The server must recompute/validate matching and proposed writes rather than trust client-supplied
IDs, minima, counters or booleans. Preview is not a lock and does not authorize overwriting newer data.

Revalidate the accepted set before applying, including offer versions, current primary-contact
membership, candidate uniqueness, terms and relevant site state. A concurrent insertion of another
eligible offer or a change to the linked Webmaster must not bypass matching checks. Database-level
concurrency/transaction protection is required, not only an early in-memory version comparison.

Apply the accepted set atomically with its audit and saved operation result. Input exceptions
are deliberately excluded; a new concurrency conflict after preview aborts the accepted set with
a clear conflict response and no partial offer/history/result commit. Preserve input so the user
can refresh the preview. Compute actual before/after minima consistently with that committed set.

Follow existing PostgreSQL xmin optimistic-concurrency patterns. Update the parent offer version
when child prices change, just as the current manual editing path does. Do not use timestamps
as concurrency tokens or overwrite protected metadata to force a save.

Record actor, UTC time, actual old/new business values and a clear `Bulk price update` source
in offer history. Include term/status changes and an operation reference. Do not create Site
history entries because no catalog records are written.
Include original quote metadata and the conversion snapshot for foreign input. A later daily
FX-reference refresh must not alter this history, audit timestamps, or parent-offer versions.

An Apply operation must have a retry identity. Retrying a committed request, including after a
lost HTTP response, returns its existing result and does not repeat writes or history. A newly
submitted identical price list produces No changes except for any genuine status/term correction.

## 10. Step 3 — saved result and exports

Save enough immutable operation detail to reopen the result after refresh or restart and download
its reports later: scope, actor/time, selected types, domain outcomes, actual changed values,
matching offer IDs, term/status corrections, exceptions, affected keys and before/after minima.
A notification alone or client-only sessionStorage is not a saved result.

Provide a result URL and a small recent-operation entry point accessible from this workflow.
Do not turn operation history into a task system. Reuse the existing styling, pagination and
history conventions. Existing ImportLog counters alone do not retain the required detail.

The result table contains Domain, service/term, offer before/after value, minimum before/after
value, and Catalog recalculation needed. Term/status changes must also be visible, including
reactivation without an amount change. No catalog-processing status, assignee, action or completion
column is needed. Retain No changes and Exceptions as separate filters/lists in the saved result.

### A. Download update report

Generate an XLSX report of the entire operation, across all pages. A readable long-form Changes
sheet has one row per changed service/term, with Domain, Offer ID, selected Primary email,
old/new terms, old/new price/availability, old/new offer status, minimum before/after, and the
catalog indicator. Include standalone term/status changes even when no numeric amount changed.
Keep operational prices in USD; include source quote/rate metadata as separate audit detail
when the operation used foreign input. Do not add it to existing unrelated export layouts.
Store operation ID, actor, time and scope once in a summary area/sheet. Include separate No changes
and Exceptions sheets when non-empty. Preserve source row references and reasons for exceptions.

### B. Download catalog calculation data

This XLSX contains purchase-price inputs for an administrator's spreadsheet markup calculation.
It is neither selling-price output nor a ready-to-import CSV. No markup assumptions or formulas.

- One row per Domain + Term after grouping affected mapped keys.
- Use the operation's post-update minimum from all eligible suppliers.
- Populate only affected service columns whose minimum changed and is still numeric.
  Leave other service cells blank; do not fill every available price for the domain.
- Label columns explicitly as purchase prices, e.g. Main purchase USD and Casino purchase USD.
  Do not disguise purchase amounts as the catalog import's selling-price columns.
- Put keys whose minimum became null on a separate No purchase price sheet, with Domain,
  Term, service and previous minimum. Do not substitute zero or infer catalog NO.
- Include explicit terms, operation/time metadata and informational quarantine state. Term
  corrections can produce rows for more than one term; do not flatten them by domain alone.
- If no key requires recalculation, disable this download with a clear explanation. Never
  populate it with unchanged minima merely because the supplier's own price changed.

Use typed numeric Excel cells for money and text for domains/IDs/statuses. Explain that blanks
in this calculation workbook mean no recalculation input, not a command to clear catalog prices.
Preparing a correct existing Sites update-import file is the administrator's responsibility.
Do not alter that import's blank-cell or repeated-domain rules in this feature.

The operation report stays historical. When reopening/downloading calculation inputs, detect and
show that relevant purchase data changed again since the operation; offer a refreshed calculation
view/export with a new as-of time rather than silently presenting stale figures as current.
Keep that refresh separate from the immutable operation report; it must not apply price updates.
Refresh only the originally affected keys, keeping the original affected-service mask; later
unrelated changes belong to their own operations. Recompute their current minima and put newly
missing values on the No purchase price sheet, never in numeric cells as zero.

## 11. Suggested implementation sequence and limits

1. Implement server-side normalization, matching, preview classification and focused patching.
2. Add atomic Apply, concurrency, history and persisted results/retry protection.
3. Add the three-step UI, exception counters, copying and conflict recovery.
4. Add before/after purchase comparison and both report downloads.
5. Update current product documentation once the feature is implemented and verified.

Keep this within the existing ASP.NET/EF/PostgreSQL and React/MUI architecture. Exact entity,
DTO and route names are implementation choices. Do not introduce a scheduler, message broker,
generic workflow engine, task framework, new UI library, or a broad manual-edit refactor.

Implementation defaults not independently specified by the user are: existing manage/export
permission boundaries, the existing input ceiling, strict common-price-term consistency, identical
duplicate collapse, all-or-nothing application of the accepted set, and retry-safe saved operations.
They make this brief executable while preserving the agreed simple workflow. If implementation
reveals a conflict with a product rule, stop and clarify rather than silently widening the feature.

## 12. Acceptance and verification checklist

Backend tests should cover these business behaviors using the repository's Arrange/Act/Assert
style; use PostgreSQL integration tests for actual concurrency and transaction guarantees:

- [ ] Exact normalized domain/contact matching across technical Webmaster records; no raw-Contact
  substring targeting, relinking, merging, or creation of missing entities.
- [ ] One selected-term candidate, one No term candidate, both together, duplicate candidates,
  another known term only, malformed terms and internally inconsistent price terms.
- [ ] Both Active/Inactive candidates participate in uniqueness; No term is not a global wildcard.
- [ ] Selected blank/Main YES/Main NO/CLEAR/invalid amounts rejected; optional YES/NO accepted;
  existing maximum/precision rules preserved; unselected fields/details unchanged.
- [ ] The common input currency uses the existing [currency rules](../business-requirements.md#webmaster-quote-currencies),
  preserving original quotes/payment details and validating converted amount bounds.
- [ ] Currency/source-amount changes with equal resulting USD are audited without inventing
  a purchase-minimum change; daily FX refresh never reprices existing offers or catalog data.
- [ ] No term correction updates common structured terms, including unselected price rows,
  while retaining their amounts/statuses and imported raw term.
- [ ] Numeric input reactivates even with an unchanged amount; YES/NO-only input preserves status;
  quarantine and every catalog field remain byte/value-equivalent after the operation.
- [ ] Identical duplicate input collapse, conflicting duplicates, per-domain counters, and
  all/category exception copying across pagination.
- [ ] Valid no-op changes no audit timestamps/history; invalid and unmatched rows are not written.
- [ ] Competing supplier minima, exact-term matching, service mapping, null transitions,
  unchanged minimum, reactivation, and old/new keys on term correction.
- [ ] Unselected mapped types affected by term/status changes are included; unsupported catalog
  types and LinkDetective data are not used as purchase inputs.
- [ ] A missing catalog selling price/term and a lost numeric minimum never reject valid offer edits.
- [ ] A new conflicting match, stale offer version, changed contact membership or concurrent
  quarantine operation cannot produce partial writes or a misleading committed report.
- [ ] Retry after commit returns the original operation without duplicate history or writes.
- [ ] Persisted results survive a new session/process; later changes are signalled on calculation
  download without rewriting historical reports or changing business data.
- [ ] Reopen generated workbooks and assert sheets, types, service columns, exact before/after
  values, affected-only numeric population, missing-purchase cases and operation metadata.
- [ ] Authorization separately protects preview/apply/result reads and all report downloads;
  no unauthorized data is exposed by guessed IDs or stale frontend responses.

For frontend verification, follow AGENTS.md: use existing checks and focused manual verification;
do not proactively add frontend unit tests. Verify large paste/pagination, all three steps,
counter filters/copying, error recovery, stale preview invalidation, unsaved-input navigation,
double-click prevention, permission-sensitive controls and reopening the result URL.

Run the relevant repository build/lint/test quality gates during implementation. Update
`docs/business-requirements.md` to concise shipped rules, including this workflow's explicit
reactivation-without-quarantine-change behavior. Update the English Roles & permissions guide
for the new capabilities while preserving role boundaries. Record any actual significant data
correction in `docs/data-migrations.md` only if implementation performs one; ordinary new
operation/result tables alone do not justify a data-repair journal entry.
