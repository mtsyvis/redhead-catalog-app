# Webmaster Search and Offer Sourcing Plan

Status: Iteration 1 implemented. Later iterations remain planning-only until added to code, tests, and `docs/business-requirements.md`.

Last updated: 2026-08-31.

## Purpose

This document preserves the agreed requirements and technical direction for webmaster search, offer-source selection, future catalog price updates, bulk offer/site operations, webmaster normalization, and a future combined Sites + Webmaster Offers import.

Iteration 1 was implemented on 2026-08-25. The future sections in this document do not authorize migrations, import changes, catalog price updates, bulk operations, or webmaster normalization.

## Confirmed page strategy

The two search workflows remain separate for now:

* `/webmaster-offers` keeps its separate domain search and comparison workflow. A later UX refinement aligned its search field with Sites Catalog and made it auto-run after a 400 ms debounce; Enter runs the search immediately.
* A new `/webmasters` page provides Contact-based webmaster search.

Keeping both pages makes it possible to compare the domain-centric and webmaster-centric workflows using real usage before deciding whether both remain or are consolidated later.

The selected layout for the new page is **Variant B: progressive full width**:

1. Contact search at the top.
2. Full-width table of matching webmaster identities.
3. Compact summary of the selected webmaster.
4. Full-width domain groups with the selected webmaster's offers.

The rejected master-detail alternative kept matching webmasters in a narrow left column and offers on the right. It was not selected because it leaves too little horizontal space for raw price comparison.

## Iteration 1 scope

**Implementation status: complete.** The read-only backend API, authorized `/webmasters` page, backend tests, and current business requirements now implement this scope. Candidate-source selection was removed on 2026-08-31 so the page remains a focused search-and-review tool until webmaster profiles and a real pricing workflow exist.

Iteration 1 is deliberately read-only and small:

1. Search webmasters by any substring match in Contact.
2. Select one matching webmaster result.
3. View all offers for that webmaster and the related catalog domains.

Iteration 1 does not:

* update catalog prices;
* calculate markup;
* change offer status;
* quarantine or restore sites;
* persist a bulk operation;
* normalize or merge webmasters;
* change existing imports;
* change the domain-centric Webmaster Offers data and comparison behavior.

The page intentionally has no candidate-source selection. A selection without a downstream preview or update action was confusing and provided no business value. Source selection remains deferred until webmaster profiles and the future pricing workflow are implemented. That workflow will need selection per `Domain + CatalogPriceType + StructuredTerm`, because different offers may be best for different services.

## Current behavior and data findings

### Existing Webmaster Offers page

The current page searches one normalized domain through `GET /api/webmaster-offers?domain=...` and returns all offers for that site. It already compares offers from multiple webmasters in the context of one domain. Its search control follows the Sites Catalog full-width field pattern, auto-runs after a 400 ms debounce, supports immediate Enter submission, and has no separate Search button.

Iteration 1 adds a dedicated API for Contact substring search and for loading all domains and offers associated with a selected webmaster. The existing domain endpoint and page remain unchanged.

### Webmaster creation during import

Webmaster Offers import currently:

1. Reads `ContactRawText` from the source row.
2. Trims it, collapses whitespace, and lowercases it into `NormalizedContactRawText`.
3. Reuses a `Webmaster` only when normalized contact is exactly equal.
4. Otherwise creates a new `Webmaster` with the raw contact, normalized contact, and best-effort `PrimaryEmail`.
5. Does not match or merge webmasters by `PrimaryEmail`.

`Webmasters.NormalizedContactRawText` has a unique database index.

### ContactRawText source semantics

Both `Webmaster` and `SiteWebmasterOffer` currently store `ContactRawText`. Do not remove either field in Iteration 1.

Until a canonical profile model exists, treat them as follows:

* `SiteWebmasterOffer.ContactRawText` is the immutable source-row snapshot for the specific offer and the safest evidence field for substring search and audit/review.
* `Webmaster.NormalizedContactRawText` is the current import identity key.
* `Webmaster.ContactRawText` is the first representative raw value used to create that identity; it is not sufficient as the only long-term business source of truth.
* `Webmaster.PrimaryEmail` is a best-effort display/search aid, not a verified identity key.

Iteration 1 should search `SiteWebmasterOffer.ContactRawText`, then aggregate matching offers by `WebmasterId`.

### Empty Contact implementation gap

The current importer does not require `ContactRawText`. Empty Contact is normalized to an empty string, so multiple empty-contact rows can reuse one technical webmaster identity.

The expected product rule is that Webmaster Offers import rejects an empty Contact. This is a known gap outside Iteration 1. Existing production data should be checked before bulk actions are implemented.

## Iteration 1 UX requirements

### Contact search

Recommended behavior:

* automatic search after a short debounce, with no Search button or explicit submission step;
* case-insensitive substring matching;
* trimmed query;
* minimum three non-whitespace characters;
* server-side pagination and deterministic ordering;
* clear action resets query, candidates, selected webmaster, workspace, and errors;
* no raw-data export controls.

Each search result represents one current `WebmasterId` and shows:

* `PrimaryEmail`, or a clear not-detected value;
* representative Contact text;
* matching offer Contact snippet where useful;
* total offer count;
* active offer count;
* distinct catalog-domain count;
* latest offer/update date.

Selecting a result triggers a separate details request. Search results must not preload all offers for all matches.

### Selected webmaster workspace

The selected webmaster area is grouped by normalized catalog domain. Each domain group shows:

* domain;
* exceptional catalog state when the site is missing or quarantined; normally available sites have no status badge;
* all offers for the selected webmaster;
* offer status;
* structured and raw term;
* raw prices and availability values;
* Contact evidence and enough metadata to distinguish repeated offers;

Offers and catalog domains should be shown together rather than as unrelated tables.

### Required states

Design and implementation must cover:

* empty or too-short query;
* no matches;
* many matches and pagination;
* loading and retry;
* missing primary email;
* multiple Contact lines/emails/comments;
* selected webmaster no longer found;
* one domain with multiple offers;
* inactive offers;
* quarantined related site;
* authorization failure.

## Iteration 1 implemented technical design

A dedicated `WebmasterSearchService` and `WebmastersController` keep the Contact workflow separate from the domain-centric `WebmasterOffersService`.

Implemented endpoints:

```text
GET /api/webmasters/search?contact={query}&page={page}&pageSize={pageSize}
GET /api/webmasters/{webmasterId}/workspace
```

Implemented search response:

```text
items[]:
  webmasterId
  primaryEmail
  representativeContactRawText
  matchingContactSnippet
  offerCount
  activeOfferCount
  domainCount
  latestOfferUpdatedAtUtc
page
pageSize
total
```

Implemented workspace response:

```text
webmaster:
  webmasterId
  primaryEmail
  representativeContactRawText
  offerCount
  activeOfferCount
  domainCount
domains[]:
  domain
  siteFound
  isQuarantined
  quarantineReason
  otherWebmasterOfferCount
  offers[]
```

Query requirements:

* Search from `SiteWebmasterOffers.ContactRawText`.
* Filter by case-insensitive substring.
* Group and count server-side by `WebmasterId`.
* Do not materialize all matching offers before grouping.
* Load details only for the selected webmaster.
* Load sites, offers, prices, and display data without N+1 queries.
* Count offers from other webmasters for all workspace domains in one aggregate query. Return only the count per domain, not the other offers' sensitive details.
* Search uses translated database-side lowercase substring matching. It returns no rows without querying webmaster data when the trimmed input is shorter than three characters.
* Result ordering is latest offer update descending and then `WebmasterId`; page size is constrained to 1-100.
* Workspace eager loading uses a bounded set of split queries for the selected webmaster, sites, prices, and mailboxes, avoiding N+1 behavior and cartesian join expansion.
* No trigram index or migration was added because no representative production SQL/query plan established a need for one. Production performance should be measured before adding an index.

Authorization:

* The new read-only page should provisionally reuse `WebmasterOffersReadAccess`.
* Future bulk mutations must be restricted server-side to `Admin` and `SuperAdmin` through a separate policy.

Frontend implementation is limited to a new page/route, navigation entry, API service/types, result table, and selected-webmaster workspace. Search runs automatically after a 400 ms debounce. The UI does not show a Search button or a minimum-length notification; the three-character minimum remains an internal query guard. Clear resets the query, results, selected webmaster, workspace, and errors.

### Iteration 1 verification coverage

Backend tests cover:

* case-insensitive partial Contact matching;
* grouping and total counts by `WebmasterId`;
* deterministic server-side pagination;
* empty and shorter-than-three-character input;
* all-offer workspace loading and domain grouping;
* active/inactive offers and quarantined catalog sites;
* raw term, raw Contact, raw price values, and availability statuses;
* missing webmaster behavior;
* the `WebmasterOffersReadAccess` policy on both endpoints.

No new frontend unit tests were added, following repository instructions. Frontend verification uses lint, TypeScript/Vite build, the existing test suite, and focused manual browser verification.

## Future price sourcing and catalog updates

The system must eventually support choosing the best prices from multiple offers belonging to one or more webmasters.

Preliminary definition of best numeric source: the lowest numeric raw offer price for the same:

* normalized domain;
* mapped catalog price type;
* structured term.

The system recommends the best source, but the user can choose another offer manually.

Confirmed price mapping:

| Offer price type | Catalog price type | Action |
| --- | --- | --- |
| `Main` | `Main` | Map |
| `Casino` | `Casino` | Map |
| `Crypto` | `Crypto` | Map |
| `Dating` | `Dating` | Map |
| `LinkInsertion` | `LinkInsertion` | Map |
| `LinkInsertion18Plus` | `LinkInsertionCasino` | Map |
| `Banner` | — | Skip |
| `Banner18Plus` | — | Skip |
| `HomepageTextLink` | — | Skip |
| `HomepageTextLink18Plus` | — | Skip |

Catalog prices will use a future markup formula. Do not assume raw price is copied 1:1. Formula, rounding, and thresholds will be specified before this phase.

Future update workflow:

1. Assemble candidate offers from one or more webmaster identities.
2. Match mapped price types and exact structured terms.
3. Recommend the lowest comparable numeric source.
4. Allow manual source override.
5. Apply the future markup formula to calculate a proposal.
6. Create a missing catalog price row for a valid selected term when needed.
7. If the existing catalog price is lower than the proposal, unselect the row by default and show a warning.
8. Open a dedicated preview/review page.
9. Allow row exclusion and manual proposed-price corrections.
10. Revalidate source offers and catalog prices on confirmation.
11. Apply only confirmed changes with audit information.

Preliminary source-value preference:

1. Numeric price is preferred; lower comparable numeric price is better.
2. `YES` is preferred over `NO` when no numeric price exists.
3. Exact transfer/ranking rules for `YES`, `NO`, `Unknown`, and details-only values remain deferred.

## Term requirements

Terms match exactly:

* No term to No term;
* Permanent to Permanent;
* `N years` to the same `N years`;
* future `N months` to the same `N months`.

Future catalog updates may create a missing price entry for the selected term.

Month support is an end-to-end prerequisite. Current code and database constraints support only No term, Permanent, and finite years. Month work must update:

* `TermUnit`;
* `PricingTerm` keys, parsing, labels, sorting, and validation;
* Site and offer database constraints;
* Webmaster Offers and Sites import parsers;
* write validators and API DTOs;
* frontend term utilities/forms;
* tests and business requirements.

## Future bulk lifecycle phase

The plan includes:

* bulk offer deactivation;
* bulk offer activation;
* bulk site quarantine;
* affected-record preview;
* row-level selection/exclusion;
* persistent bulk operation record;
* item-level before/after snapshots;
* result review;
* selective undo;
* conflict handling when records changed after the operation;
* Admin/SuperAdmin-only authorization.

Confirmed rule: do not quarantine a site when it still has an active offer from another webmaster after the selected webmaster's offers are deactivated.

Do not assume mass activation automatically removes site quarantine. That rule remains open.

## Future WebmasterProfile normalization

Do not destructively merge current `Webmaster` rows while import matches by exact normalized Contact. Otherwise a future nonduplicate row can recreate the removed identity.

Preferred model:

```text
WebmasterProfile
  Id
  DisplayName
  PrimaryEmail
  NormalizedPrimaryEmail
  Notes
  Status
  CreatedAtUtc
  UpdatedAtUtc

WebmasterProfileMember
  WebmasterProfileId
  WebmasterId
  AddedAtUtc
  AddedBy
```

The current `Webmaster` remains an imported contact identity. A profile groups multiple identities without deleting source records or changing historical offer fingerprints.

### Proposed automatic profile migration

Run profile creation only after the complete webmaster-offer dataset has been loaded and audited.

The proposed schema and data migration should:

1. Create `WebmasterProfiles` and `WebmasterProfileMembers` with unique membership for each `WebmasterId`.
2. Normalize non-empty `Webmaster.PrimaryEmail` by trimming and lowercasing it.
3. Create one profile for each distinct normalized primary email.
4. Link every current `Webmaster` with that exact normalized primary email to the same profile.
5. Preserve all current `Webmaster`, `SiteWebmasterOffer`, `ContactRawText`, fingerprint, and audit data unchanged.
6. Leave identities with no detected primary email unprofiled for later manual normalization rather than guessing.
7. Produce pre-migration and post-migration counts for profiles, linked identities, unprofiled identities, unusually large email groups, domains, and offers.

`PrimaryEmail` is a best-effort extracted value, not a verified identity key. Exact primary-email grouping is the preferred automatic baseline, but generic or shared mailboxes can still create false-positive groups. Before applying the data migration to production, review a dry-run report of large groups and known shared-address patterns and take a database backup.

The initial migration must not change current import matching. A later iteration can decide whether newly imported contact identities should be attached automatically to an existing profile by normalized primary email.

## Future combined Sites + Webmaster Offers import

Intent:

* Header combines the Webmaster Offers contract with mandatory Sites insert fields.
* If a normalized domain does not exist, create the site and add it to the catalog.
* The same row creates its webmaster offer and raw prices.
* Initial catalog prices use the same future markup formula as the manual workflow.
* This supports sites that currently live in Excel as webmaster offers before being added to the application.

Do not finalize the header by simply concatenating the two current headers. Domain and Term overlap, and current Sites import documentation is inconsistent about whether Language is optional or part of required base order.

Before implementation, define:

* exact ordered header;
* existing-site behavior;
* row atomicity when site and offer validation differ;
* duplicate domains with different offers;
* duplicate fingerprints;
* Contact validation;
* year/month term validation;
* location/metric validation;
* mapped/unmapped offer prices;
* numeric/`YES`/`NO`/Unknown behavior;
* markup and rounding;
* lower existing catalog prices;
* transaction/batch/idempotency rules;
* warnings, invalid rows, downloads, result counters, and logs;
* large-file performance.

Preferred direction: row-level atomicity for a new site and its required initial offer. Do not leave a new catalog site without its offer when the offer half of that row fails.

## Resolved Iteration 1 decisions

1. Iteration 1 has no candidate-source selection; source selection is deferred until profiles and the pricing workflow exist.
2. The read-only page is visible to every role with `WebmasterOffersRead`.

## Open questions for future iterations

1. How are candidates from multiple webmasters assembled for future price sourcing: multi-select results, add-to-comparison, or the existing domain-centric page?
2. What is the exact order between `NO`, `Unknown`, and details-only values?
3. Can mass activation restore site availability, and under what conditions?
4. What exact combined-import header and existing-site behavior are required?

## Testing direction

Iteration 1 backend tests cover substring matching, grouping by `WebmasterId`, counts, pagination, minimum query validation, domain grouping, repeated offers, inactive/quarantined states, workspace data, missing webmasters, and authorization. Future iterations must add coverage for their mutation and pricing behavior.

Follow repository quality gates. Do not proactively add frontend unit tests unless the implementation task explicitly asks for them; use lint, build, existing tests, and focused manual verification otherwise.
