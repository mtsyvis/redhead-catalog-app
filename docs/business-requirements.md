# Business Requirements

## Purpose

Redhead Sites Catalog is a private catalog application for Redhead Digital Agency.

The product helps internal users browse, filter, update, import, and export a large list of advertising sites. It replaces fragile spreadsheet-based workflows for catalog search, site availability, price data, and client-facing exports.

This file is the current source of truth for product behavior and business rules. It should stay concise and practical. Do not store implementation details, setup commands, or deployment notes here.

## Documentation status

* This file describes current product behavior and accepted requirements.
* `README.md` describes project setup and development commands.
* `AGENTS.md` describes how AI coding agents should work with this repository.
* `docs/deployment.md` describes production deployment and operational notes.

Legacy planning files and old specs are historical context only. They must not override this document.

## Users and roles

The app has four roles:

* `SuperAdmin`
* `Admin`
* `Internal`
* `Client`

General rules:

* Users authenticate with email and password.
* There is no public self-registration.
* Each user has one role.
* Disabled users cannot log in or access API endpoints.
* Users are soft-disabled with `IsActive = false`; physical deletion is not allowed.
* Authorization must be enforced server-side. UI hiding is only a convenience layer.

### SuperAdmin

`SuperAdmin` has full administrative control.

Current rules:

* Only `SuperAdmin` can create new users.
* Only `SuperAdmin` can update role export limits.
* Only `SuperAdmin` can update per-user export limit overrides.
* `SuperAdmin` export access is unlimited and must not be editable in the UI.
* `SuperAdmin` can reset passwords and disable users according to server-side authorization rules.

### Admin

`Admin` is an internal management role, but not a full system owner.

Current rules:

* `Admin` can access admin areas allowed by backend policies.
* `Admin` can run catalog imports and update catalog data where backend policies allow it.
* `Admin` must not be able to create users.
* `Admin` must not be able to change role export limits.
* `Admin` must not be able to change per-user export limit overrides.

### Internal

`Internal` is a read-oriented internal role.

Current rules:

* Can browse and filter the sites catalog.
* Can export if export is enabled for the role or user.
* Cannot manage users, role settings, imports, or catalog editing unless a future requirement explicitly changes this.

### Client

`Client` is a read-oriented external/client role.

Current rules:

* Can browse and filter the sites catalog.
* Can export if export is enabled for the role or user.
* Cannot access admin, import, user-management, or catalog-editing features.

## Password provisioning

New users are provisioned by an administrator flow, not by self-registration.

Rules:

* The system generates a temporary password.
* Temporary password is displayed once after creation or reset.
* Temporary password must not be logged or stored in plain text.
* New users must change password on first login.
* After password reset, `MustChangePassword` must be set to `true` again.
* While `MustChangePassword = true`, the user must be forced to the change-password flow and blocked from normal app pages.

## Export limits

Exports are controlled by role-level settings and optional per-user overrides.

Export modes:

* `Disabled` — user cannot export.
* `Limited` — user can export up to a configured row count.
* `Unlimited` — user can export without a row cap.

Rules:

* Role settings define the default export policy for each role.
* A per-user export override may replace the role default.
* Per-user override wins over role setting.
* `SuperAdmin` is always unlimited and must not be editable.
* Only `SuperAdmin` can edit role export limits or per-user export overrides.
* Export must use the same current filters/search/multi-search context that the user sees in the grid.
* If export is limited, the exported file must be capped by the effective limit.
* If export is disabled, the UI should prevent export and the backend must reject export.
* If export is truncated by a limit, the user should receive clear feedback.

## Sites catalog

The catalog is expected to contain tens of thousands of sites. Search, filtering, sorting, paging, imports, and exports must be designed for that scale.

Core site fields:

* `Domain`
* `DR`
* `Traffic`
* `Location`
* `PriceUsd`
* `PriceCasino`
* `PriceCasinoStatus`
* `PriceCrypto`
* `PriceCryptoStatus`
* `PriceLinkInsert`
* `PriceLinkInsertStatus`
* `PriceLinkInsertCasino`
* `PriceLinkInsertCasinoStatus`
* `PriceDating`
* `PriceDatingStatus`
* `NumberDFLinks`
* `TermType`
* `TermValue`
* `TermUnit`
* `Niche`
* `Categories`
* `LinkType`
* `SponsoredTag`
* `IsQuarantined`
* `QuarantineReason`
* `QuarantineUpdatedAtUtc`
* `LastPublishedDate`
* `LastPublishedDateIsMonthOnly`
* `CreatedAtUtc`
* `UpdatedAtUtc`

### Domain

`Domain` is the unique catalog key.

Rules:

* Store and match domains in normalized form.
* Domain matching for imports and multi-search must use exact normalized equality.
* Single search may support partial domain search for normal browsing.

Domain normalization rules:

* Trim whitespace.
* Remove `http://` or `https://`.
* Remove leading `www.`.
* Remove path, query string, and fragment.
* Remove trailing slash.
* Lowercase.

Use the same normalization in:

* sites import
* sites update import
* quarantine import
* last published date import
* single search
* multi-search
* export logic where search context is involved

### Price fields

`PriceUsd` is nullable.

Rules:

* Empty `PriceUsd` must be stored as empty/null, not as `0`.
* UI must display empty `PriceUsd` as `-`.
* During sites import and sites update import, if `PriceUsd` is empty, at least one service-specific price/availability value must still be valid and present.
* Invalid price data must create row-level validation errors.
* Empty input in an import file means empty value, not implicit zero.

### Service-specific prices

Service-specific columns:

* `PriceCasino`
* `PriceCrypto`
* `PriceLinkInsert`
* `PriceLinkInsertCasino`
* `PriceDating`

Each service-specific price has an availability status:

* `Unknown`
* `Available`
* `NotAvailable`

Rules:

* A numeric value means the service is available with that price.
* Text markers such as `NO`, `N/A`, `NA`, `–`, `NONE`, `NOT AVAILABLE` mean the service is not available.
* Invalid text that is not a supported not-available marker and not a number must be a validation error.
* Do not represent service availability only as nullable decimal; availability and price are separate concepts.
* UI must not mislead users by showing unavailable services as zero-price services.

### Additional site fields

`NumberDFLinks` is nullable. When present, it must be a positive whole number.

Term is stored as `TermType`, `TermValue`, and `TermUnit`.

Current term rules:

* Empty term means unknown: all three term fields are empty.
* `permanent` means `TermType = Permanent` and the value/unit fields are empty.
* `N year` or `N years` means `TermType = Finite`, `TermValue = N`, and `TermUnit = Year`.
* Only positive integer year terms are currently valid. Month/day/lifetime/abbreviated values are not supported.

### Quarantine

Quarantine marks sites that should currently be treated as unavailable.

Rules:

* `IsQuarantined = true` means the site is displayed as unavailable.
* `QuarantineReason` is optional.
* `QuarantineUpdatedAtUtc` is updated when quarantine state is changed by import or edit.
* When quarantine is turned off manually, `QuarantineReason` should be cleared.
* Quarantined sites remain in the catalog and can still appear in filtered results depending on the selected quarantine filter.

### LastPublishedDate

`LastPublishedDate` stores the last known Redhead publication date for a site.

Rules:

* Field is nullable.
* Field is visible to all users.
* Field is read-only in the normal site edit UI.
* Field is updated only via Last Published Date import.
* If date is missing, UI displays: `Last publication before January 2026`.
* Null values sort last when sorting by last published date.
* `LastPublishedDateIsMonthOnly` distinguishes exact dates from month-only dates.
* Exact dates should display as `DD.MM.YYYY`.
* Month-only dates should display as `MMMM YYYY`.
* Filters for this field should use month/year semantics, not day-level precision.

## Sites table behavior

The main sites page is the primary working screen.

Rules:

* All authenticated users can access the sites page.
* Data grid uses server-side paging, sorting, filtering, and search.
* Large result sets must not be fully loaded into the browser.
* Nullable or unavailable values should use clear placeholders such as `-`.
* Sorting by service-specific price fields keeps available services first in both ascending and descending order; not-available and unknown services sort after available services.
* Row edit actions are visible only to roles allowed to edit, and backend authorization must enforce the same rule.

Main filters:

* Domain search
* DR range
* Traffic range
* Price range
* Location multi-select
* Casino availability
* Crypto availability
* Link Insert availability
* Link Insert Casino availability
* Dating availability
* Quarantine status: all / only quarantined / exclude quarantined
* Last publication date range/month filter

## Multi-search

Multi-search lets users paste many domains/URLs and see which ones exist in the catalog.

Availability:

* Available to all authenticated users.

Input rules:

* Uses the same search area as normal search, with a Multi-search mode/toggle.
* Input accepts domains or URLs separated by spaces and/or new lines.
* Maximum input count: 500.
* Inputs are normalized before matching.
* Matching is exact normalized `Domain` equality, not substring search.
* Duplicate inputs after normalization are detected, removed from search execution, and reported to the user.

Display rules:

* Found rows are shown in the same sites grid as normal search results.
* Not found rows are appended at the end only when no filters are active.
* Not found rows contain only `Domain`; all other columns show placeholders.
* When any filters are active, not found rows are hidden.
* If not found rows are hidden, show a clear hint such as: `Not found (X) hidden while filters are active` and provide a clear-filters action.

Export rules:

* If no filters are active, export includes found rows plus not found domains appended at the end.
* If filters are active, export includes only filtered found rows.
* Not found domains must not be included in export while filters are active.
* Effective export limits still apply.

## Imports

Imports must be predictable, validated, and safe for large CSV files.

General rules:

* CSV is the supported import format.
* XLSX is not current scope.
* Validate required headers strictly where the import depends on column meaning/order.
* Support existing delimiter behavior consistently.
* Validate row-level errors and return useful feedback instead of crashing the whole import.
* Normalize domains before matching or inserting.
* Avoid N+1 database operations; batch database reads and writes.
* Import results should show summary counts and downloadable details where supported.
* Import logs must record who ran the import, when it happened, import type, and summary counts.
* Duplicate domains in an input file should have explicit behavior. Current update-style imports use last valid row wins.

### Sites import

Purpose: add new sites to the catalog.

Required columns, in order:

1. `Domain`
2. `DR`
3. `Traffic`
4. `Location`
5. `PriceUsd`
6. `PriceCasino`
7. `PriceCrypto`
8. `PriceLinkInsert`
9. `PriceLinkInsertCasino`
10. `PriceDating`
11. `Niche`
12. `Categories`
13. `LinkType`
14. `NumberDFLinks`
15. `SponsoredTag`
16. `Term`

Rules:

* Add-only import.
* Existing domains are skipped and reported.
* New domains are inserted.
* Domain is normalized before uniqueness checks.
* Duplicate domains inside the input are reported.
* Invalid rows are not inserted.
* Empty rows are skipped.
* Import should support large catalog files.

### Sites update import

Purpose: mass-update existing sites by domain.

Rules:

* Uses the same columns as sites import.
* `Domain` is the lookup key and must never be changed by the import.
* Updates existing sites only.
* Unknown domains are reported as unmatched; they are not inserted.
* Last valid row wins for duplicate domains in the file.
* Invalid rows are reported and not applied.
* Updates must preserve quarantine and last published fields unless explicitly part of the import behavior.

### Quarantine import

Purpose: mark existing sites as unavailable.

Required columns, in order:

1. `Domain`
2. `Reason`

Rules:

* Updates existing sites only.
* Domain is normalized and matched by exact equality.
* Matched rows set `IsQuarantined = true`.
* `Reason` is optional and stored as `QuarantineReason` when provided.
* `QuarantineUpdatedAtUtc` is updated for matched rows.
* Unknown domains are reported as unmatched.
* Invalid rows are reported.
* Duplicate domains use last row wins behavior.

### Last Published Date import

Purpose: update last known Redhead publication date for existing sites.

Required columns, in order:

1. `Domain`
2. `LastPublishedDate`

Rules:

* Updates existing sites only.
* Domain is normalized and matched by exact equality.
* Unknown domains are reported as unmatched.
* Invalid dates are row-level errors.
* Empty `LastPublishedDate` is invalid.
* Duplicate domains use last row wins behavior.
* Accepted date formats are currently limited to invariant-culture English formats:
  - full date: `DD.MM.YYYY`, for example `15.01.2026`
  - full month name + year: `MMMM YYYY`, for example `January 2026`
  - short month name + year: `MMM YYYY`, for example `Jan 2026`
* Month-only values are stored as the first day of the month plus `LastPublishedDateIsMonthOnly = true`.

## Exports

Exports produce CSV files from the current catalog context.

Rules:

* Export respects current filters, search, sorting where supported, and multi-search mode.
* Export includes all user-visible site data needed for business use.
* Export must enforce the user's effective export policy.
* Export actions should be logged.
* If export is truncated by limit, the user must be informed.
* Disabled export must be enforced by backend, not only by hiding the button.

## Admin UI

Admin UI exists to manage users, role export limits, and imports.

Rules:

* Admin navigation should show only sections the current user can access.
* Backend policies remain authoritative even if navigation hides a section.
* User creation UI is available only to `SuperAdmin`.
* Role settings editing is available only to `SuperAdmin`.
* Per-user export override editing is available only to `SuperAdmin`.
* `SuperAdmin` export settings are shown as unlimited and not editable.

## Branding and UI direction

The app should look like a clean internal business tool with Redhead Digital Agency branding.

Rules:

* Use the existing MUI-based design system.
* Prefer shared theme tokens and shared components over one-off styling.
* Primary text color: `#262626`.
* Accent gradient: `#FF455B` to `#FF7C32`.
* Use Outfit font where configured.
* Use rounded/pill-style controls where appropriate.
* Keep workflows simple for non-technical users.
* Do not copy the marketing website layout directly; use brand style, not marketing-page structure.

## Current non-goals

These are not current requirements unless explicitly reintroduced:

* Public registration.
* XLSX import.
* Multi-tenant account separation.
* Client-specific hidden columns.
* Full CRM/order-processing portal.
* Global mass price adjustment tool with multiplier/additive changes.
* Vendor price upsert workflow with preview/confirmation.
* Complex audit-log UI beyond what is needed for traceability.

Some of these may become future scope, but they should not be implemented accidentally during catalog work.
