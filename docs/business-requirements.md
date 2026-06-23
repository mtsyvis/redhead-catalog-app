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
* There is no public self-registration without a `SuperAdmin` invitation.
* Each user has one role.
* Each user stores one optional `DisplayName` field with a maximum length of 100 characters.
* Each user may have an optional internal `SuperAdmin` note for identifying client accounts when email/name are not enough.
* The internal `SuperAdmin` note is visible and editable only by `SuperAdmin`; `Admin`, `Internal`, `Client`, profile/current-user, auth, export analytics, audit context, and other non-SuperAdmin-specific responses must not expose it.
* A user's profile is complete only when `DisplayName` is non-empty after trimming.
* Existing activated users with a missing display name must complete it after login.
* Email is used as the fallback display value while `DisplayName` is missing.
* Users can update only their own display name.
* Disabled users cannot log in or access API endpoints.
* When a user's authenticated session expires or becomes invalid during protected frontend workflows, the user is redirected to `/login`, shown a session-expired message, and returned to the original page after signing in again.
* Users are soft-disabled with `IsActive = false`; physical deletion is not allowed.
* Disabled users may be reactivated only by `SuperAdmin`.
* Authorization must be enforced server-side. UI hiding is only a convenience layer.
* The admin users list is paginated and can be filtered by all users, client users, or internal users.
* Internal users in the admin users list means any user whose role is not `Client`.

### SuperAdmin

`SuperAdmin` has full administrative control.

Current rules:

* Only `SuperAdmin` can create new users.
* Only `SuperAdmin` can update role export limits.
* Only `SuperAdmin` can update per-user export limit overrides.
* Only `SuperAdmin` can create or update the internal `SuperAdmin` note on user accounts.
* Only `SuperAdmin` can change user roles.
* `SuperAdmin` can change roles only between `Admin`, `Internal`, and `Client`; `SuperAdmin` is a protected role and cannot be promoted or demoted through normal role editing.
* `SuperAdmin` cannot change their own role.
* Changing a user's role preserves any per-user export limit override; removing or changing that override is a separate explicit action.
* `SuperAdmin` export access is unlimited and must not be editable in the UI.
* `SuperAdmin` can reset passwords and disable users according to server-side authorization rules.
* `SuperAdmin` cannot disable their own account.
* The last active `SuperAdmin` account cannot be disabled.

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

## Account invitations and password provisioning

New users are provisioned by a `SuperAdmin` invitation flow. Public registration without an invitation is not allowed.

Rules:

* Creating a user requires email, role, and an optional internal `SuperAdmin` note.
* The system generates a cryptographically random single-use activation token and stores only its SHA-256 hash.
* The activation link is displayed to `SuperAdmin` once and expires 72 hours after creation.
* If the link is lost or expires, `SuperAdmin` may reissue it for a pending or expired invitation. Reissuing invalidates the previous link and starts a new 72-hour period.
* Invitation tokens and activation links must not be logged.
* Account status is exposed as `Active`, `PendingActivation`, `InvitationExpired`, or `Disabled`.
* The invited user provides the required `DisplayName` and a password satisfying the normal Identity password policy.
* Successful activation consumes the invitation, confirms the email, and signs the user in immediately.
* If a new user is created with an email that belongs to an active user, creation is rejected.
* If a new user is created with an email that belongs to a disabled user, creation is rejected and the disabled account should be reactivated instead.
* New users are created without a password or display name and cannot sign in before activation.
* Existing activated users may still use account setup to complete a required password change, display name, or both.
* After password reset, `MustChangePassword` must be set to `true` again.
* Reactivating an already activated user sets `MustChangePassword = true` and displays a temporary password once.
* Reactivating a disabled, never-activated user issues a new activation link instead.
* Reactivation preserves display name, internal `SuperAdmin` note, Google Drive connection, saved filters, table views, and user history.
* Reactivation clears per-user export limit overrides.
* Disabled `SuperAdmin` users can be reactivated only as `SuperAdmin`; disabled `Admin`, `Internal`, and `Client` users can be reactivated only as `Admin`, `Internal`, or `Client`.
* While `MustChangePassword = true` or `DisplayName` is incomplete, an activated user must be forced to `/account-setup` and blocked from normal app pages.
* Users can update their own display name from `/profile`; admins must not edit another user's display name.

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
* Users can view their own effective export limit on `/profile`.
* Export must use the same current filters/search/multi-search context that the user sees in the grid.
* If export is limited, the exported file must be capped by the effective limit.
* If export is disabled, the UI should prevent export and the backend must reject export.
* If export is disabled for the current user, `/profile` must hide the Google Drive export connection card.
* If export is disabled for the current user, `/sites` must hide the export menu.
* If export is truncated by a limit, the user should receive clear feedback.

Client-role exports also have rolling usage limits:

* Daily unique exported domains limit: default `1000`.
* Weekly unique exported domains limit: default `3000`.
* Daily export operations limit: default `20`.
* Weekly export operations limit: default `60`.
* Daily means the last 24 hours. Weekly means the last 7 days.
* These usage limits apply only to `Client` users.
* Unique-domain usage is counted per user by exported site domain. Re-exporting the same domain inside the same rolling window does not consume another unique-domain slot.
* Export operation usage is counted per user for successful and partially successful exports only. Blocked attempts are logged but do not consume operation quota.
* If only part of a requested export fits within the remaining daily or weekly unique-domain quota, the export is partial and includes only allowed site rows.
* If no requested site rows can be exported because a usage limit is reached, the backend rejects the export and does not create a downloadable file or Google Drive file.
* Google Drive exports must enforce the same row and usage limits as download exports.

## Sites catalog

The catalog is expected to contain tens of thousands of sites. Search, filtering, sorting, paging, imports, and exports must be designed for that scale.

Core site fields:

* `Domain`
* `DR`
* `Traffic`
* `Location`
* `Language`
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
* `SponsoredTag`
* `IsQuarantined`
* `QuarantineReason`
* `QuarantineUpdatedAtUtc`
* `LastPublishedDate`
* `LastPublishedDateIsMonthOnly`
* `CreatedAtUtc`
* `UpdatedAtUtc`
* `CreatedBy`
* `UpdatedBy`

### Location

Location is the product term for the site geography field. Locations can include countries and territories.

Canonical location rules:

* Canonical locations are keyed by stable location codes, plus `UNKNOWN` for empty imported Location values.
* `UNKNOWN` is a real canonical location with display name `Unknown`.
* Non-empty imported Location values that cannot be mapped are stored as unmapped with a null canonical location key and the original raw value preserved for warnings and later review.
* `Other` is not a canonical location.
* `Other` means the site has no canonical `LocationKey`.
* Static aliases map common names and codes such as `USA`, `UK`, `UAE`, and `Korea` to canonical location keys.
* Placeholder imported Location values such as `#N/A` and `N/A` map to canonical `UNKNOWN`.
* Location groups can represent regions or business groups. Group kind is not a location type.
* Location group filters use union semantics. Selecting multiple groups includes sites from any selected group location.
* Location group member locations can overlap across groups. If a user excludes a location from a selected group, that exclusion applies globally to the final location filter result, even if another selected group also contains that location.
* Location filter UI supports searching location groups and canonical locations. Search results show canonical locations once, even when a location belongs to multiple groups.
* `Unknown` and `Other` remain special location filter states and are not controlled by group member exclusions.
* Sites list and export display use canonical names: mapped locations use the canonical display name, `UNKNOWN` displays as `Unknown`, and null `LocationKey` displays as `Other`.
* In the Sites table, rows displayed as `Other` should append the preserved non-empty imported Location value when available, for example `Other - Atlantis`.
* In manual site edit, rows currently displayed as `Other` should show the preserved imported Location value so users know what unmapped value they are replacing.
* Manual site edits use the same canonical normalization. Empty or `Unknown` Location values set canonical `UNKNOWN`.

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
* availability import
* last published date import
* single search
* multi-search
* export logic where search context is involved

### Price fields

`PriceUsd` is nullable.

Term-aware pricing is being introduced as an additive backend model. The backend stores term-specific price options in `SitePriceOptions` keyed by site, price type, and normalized term key, and optional service availability in `SiteServiceAvailabilities`. Existing flat site price columns remain temporarily for compatibility until imports, editing, filtering, sorting, exports, and frontend rendering are moved to the term-aware model.

Rules:

* Empty `PriceUsd` must be stored as empty/null, not as `0`.
* `PriceUsd` must be either empty/null or greater than `0`; `0` and negative values are invalid.
* UI must display empty `PriceUsd` as `NO`.
* Price filtering and sorting use `SitePriceOptions` as the backend source of truth once term-aware pricing is enabled. With no selected term, price logic considers all terms; with a selected `TermKey`, price logic considers only matching price options.
* During sites import, price fields may all be empty or unavailable; valid rows are not rejected only because no numeric price is present.
* During sites update import, a present empty `PriceUsd` cell with a row `Term` clears that exact `SitePriceOption`; a missing price column leaves pricing data unchanged.
* During sites update import, service price and availability fields may be omitted, cleared, or set unavailable according to field-level rules; update rows are not rejected only because no numeric price remains.
* During sites import and sites update import, if a `PriceUsd` value is provided, it must be greater than `0`.
* Site exports use `SitePriceOptions` as the source of truth for the `Price USD` column. With no selected term, the exported cell contains the lowest known main price across all terms as a raw numeric Excel value. With a selected `TermKey`, the exported cell contains only that term's main price as a raw numeric Excel value. If no matching main price exists, export `—`.
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
* `AvailableWithUnknownPrice`

Rules:

* Empty import input means unknown/no data: status `Unknown`, price empty/null.
* A positive numeric import value means the service is available with that known price: status `Available`, price greater than `0`.
* `YES` in an import, case-insensitive, means the service is available but the exact price is unknown: status `AvailableWithUnknownPrice`, price empty/null.
* Text markers such as `NO`, `N/A`, `NA`, `–`, `NONE`, `NOT AVAILABLE` mean the service is not available.
* Invalid text that is not `YES`, a supported not-available marker, or a positive number must be a validation error.
* Service-specific price `0` and negative prices are invalid.
* Status and price must be consistent: `Available` requires a positive price; `AvailableWithUnknownPrice`, `NotAvailable`, and `Unknown` require an empty/null price.
* Do not represent service availability only as nullable decimal; availability and price are separate concepts.
* UI must not mislead users by showing unavailable services as zero-price services.
* UI and exports show `YES` for `AvailableWithUnknownPrice`, `NO` for `NotAvailable`, and empty/placeholder values for `Unknown`.
* Site exports use `SitePriceOptions` and `SiteServiceAvailabilities` as the source of truth for service price columns. With no selected term, if service prices exist, export the lowest known service price across all terms as a raw numeric Excel value; otherwise export `YES`, `NO`, or `—` from the service availability status. With a selected `TermKey`, export only that term's service price as a raw numeric Excel value; if that service has no price for the selected term, export `—` without falling back to another term or global service availability status.

### Additional site fields

`Language` is optional and stores the main language classification for a site.

Rules:

* Empty `Language` means language data is not available yet or the site was not processed by the parser.
* `UNKNOWN` means the site was processed but the language could not be determined.
* `MULTI` means the site has multiple main languages.
* Two-letter ISO-style language codes are stored uppercase, for example `EN`, `DE`, `FR`, `RU`, and `ID`.
* Manual site edits must normalize supported language inputs and reject invalid values instead of converting them to `UNKNOWN`.

`Categories` stores AI-generated category phrases as one comma-separated text value.

Rules:

* Categories search accepts multiple already-parsed terms.
* Each term is matched as a literal case-insensitive substring in the full `Categories` text.
* Multiple category terms use OR semantics; the category search filter is combined with other active filters using AND.
* Empty category search terms are ignored.
* In the frontend, category search input accepts comma, Enter, and newline-separated terms.
* Spaces inside category search phrases are preserved.
* Category exclude terms remove sites where `Categories` contains any excluded phrase.
* Category include and exclude terms use the same parsing, trimming, deduplication, and validation rules.

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
* Unavailable sites use a restrained red table-row treatment with a warning icon beside the domain and a red `Unavailable` status badge so managers can clearly identify sites that must not be offered to clients.
* `QuarantineReason` is optional.
* `QuarantineUpdatedAtUtc` is updated when quarantine state is changed by import or edit.
* When quarantine is turned off manually, `QuarantineReason` should be cleared.
* Quarantined sites remain in the catalog and can still appear in filtered results depending on the selected quarantine filter.

### Ahrefs monthly metrics sync

Once per UTC month, at 01:00 UTC on the first day by default, the backend updates `Traffic` and `DR` for non-quarantined sites from Ahrefs Batch Analysis using target mode `subdomains`. Zero traffic is valid. Failed or missing Ahrefs rows preserve existing site values. Ahrefs updates set `AhrefsLastSyncedAt` but do not change the normal site audit fields `UpdatedAtUtc` or `UpdatedBy`.

Metric history stores at most one snapshot per domain and actual UTC snapshot date. Scheduled and manual full runs save snapshots by default; limited manual runs do not unless requested. If a scheduled run waits for the Ahrefs usage reset and starts on a later date, the history records that actual run date rather than the configured cron date.

The catalog values known to have been refreshed for all sites on June 4, 2026 are imported once into history with source `AhrefsBaselineImport`. This is historical data only and does not create an Ahrefs sync run. The initial rollout sets `NotBeforeUtc` to July 1, 2026 at 01:00 UTC, so the first real scheduled run starts at that occurrence without relying on a manual production configuration change.

The sync is budget-aware, checks Ahrefs workspace/API-key usage before each real run, and may process only the affordable highest-traffic sites.
The configured safety buffer is used both when selecting affordable sites before the run and as the
minimum remaining-units threshold checked between batches.

Before a scheduled monthly run starts, the limits response must confirm a usable Ahrefs period.
The `usage_reset_date` must be in the future. When a previous real API sync exists, it must also
differ from that run's reset date. If Ahrefs reports an expired or previously used reset period,
the scheduler waits and checks again hourly without creating an audit run or calling Batch Analysis.
An unavailable, invalid, or expired API key creates a failed audit run with the API error and does
not call Batch Analysis. Repeated identical limits failures reuse the existing failed run instead
of adding hourly duplicates.

A successfully completed monthly full run is considered complete even when budget or the configured
site cap allowed only partial catalog coverage. It is not automatically repeated during the same
snapshot month, because another run would process the same priority-ordered sites again. The run
audit and monitoring UI must clearly show selected versus eligible sites when coverage is partial.

Ahrefs sync administration is SuperAdmin-only. Run details expose audit items through paginated backend responses so large catalog runs do not return every site item in one response.

SuperAdmin has a read-only Ahrefs monitoring UI showing current API/workspace/app-budget
availability, usage reset date, scheduler state, next scheduled run, recent sync runs, and
paginated site-level run results. The UI does not start, force, or otherwise mutate Ahrefs runs.
The monitoring status also shows spendable units after the safety buffer, the affordable and
configured next-run capacity, the limiting budget or max-sites constraint, and any full-catalog
budget shortfall. Schedule occurrences and snapshot months are displayed as human-readable UTC
dates. Run outcome is presented separately from its scope so a successful limited run is not
reported as an operational failure. Detailed budget and job configuration values are available in
a collapsed technical-details section.

### LastPublishedDate

`LastPublishedDate` stores the last known Redhead publication date for a site.

Rules:

* Field is nullable.
* Field is visible to all users.
* Field is read-only in the normal site edit UI.
* Field is updated only via Last Published Date import.
* If date is missing, API and export values remain empty/null; the Sites UI displays `-`.
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
* Users can select table views that control presentation only: visible columns, column order, density, and column widths.
* Table views must not save or reset filters, sorting, pagination, search text, or selected rows.
* System table views are readonly and defined by the frontend.
* Available system table views are `Default`, `Pricing`, `SEO`, and `Full`.
* System table views use `Standard` density by default.
* `Created Date` is available to all users. `Updated Date`, `Created By`, and `Updated By` follow the same internal-only visibility and export restrictions as `QuarantineReason`.
* Audit dates display and export as `DD.MM.YYYY`. Missing audit users display and export as `system`.
* Custom table views are stored per user and per table; users can create, update, rename, duplicate, and delete custom views.
* Custom view names must be unique per user/table ignoring case.
* The active table view selection is stored per user/table.
* Column visibility and order changes can be applied temporarily to the current table view without saving or creating a custom view.
* Unsaved table view changes are saved or reset from the view-changed workflow, not automatically when applying columns.
* The `Domain` column is always visible and cannot be hidden.
* The `Domain` column is always first; workflow/system columns such as row actions stay system-managed and last when present.
* The `Domain` column remains pinned on the left while horizontally scrolling the Sites table.
* Long text values in the Sites table stay single-line with truncation and reveal the full value on hover when truncated.
* Workflow/system columns such as row actions are not saved in table views.
* If active filters target hidden columns, the UI should warn the user and offer to show those columns or clear only those hidden-column filters.
* Sorting by service-specific price fields uses numeric `SitePriceOptions`, keeping known numeric prices first in both ascending and descending order. With no selected term, the lowest price across all terms is used; with a selected `TermKey`, only that term is used. Available-with-unknown-price (`YES`) sorts after known prices, not-available sorts after `YES`, and unknown sorts last. `YES`, `NO`, and unknown are not zero-price values.
* Row edit actions are visible only to roles allowed to edit, and backend authorization must enforce the same rule.

Main filters:

* Domain search
* Stop list domain exclusion
* DR range
* Traffic range
* Price range
* Term single-select: any term / unknown term / finite year terms / permanent
* Location multi-select
* Location group multi-select
* Include Unknown location
* Include Other location
* Language multi-select
* Topic fit:
  * Niche include multi-select
  * Niche exclude multi-select
  * Categories include substring search
  * Categories exclude substring search
  * Expand mode: Niche include OR Categories include
  * Narrow mode: Niche include AND Categories include
* Casino availability
* Crypto availability
* Link Insert availability
* Link Insert Casino availability
* Dating availability
* Quarantine status: all / only quarantined / exclude quarantined
* The default quarantine status filter includes all sites (`All Sites`) in both Single search and Multi-search modes.
* Last publication date range/month filter

Optional service availability filter rules:

* Optional service filters use the existing `ServiceAvailabilityStatus` values: `unknown`, `available`, `notAvailable`, and `availableWithUnknownPrice`.
* Empty or missing optional service filter values mean no filter.
* `available` means the service has at least one numeric `SitePriceOption`. With no selected term, any term can match; with a selected `TermKey`, the service must have a price for that term.
* `availableWithUnknownPrice`, `notAvailable`, and `unknown` use global `SiteServiceAvailabilities` and are not term-specific.
* `unknown` must not match sites that have numeric prices for that service.
* Multiple selected values for one optional service use OR semantics.
* Filters across different optional services use AND semantics.
* The `available` and `availableWithUnknownPrice` values are distinct filter states.

Sites search and filter UX rules:

* Normal domain search auto-applies after a short debounce; users do not need to press a Search button for single-search browsing.
* Multi-search does not auto-run while the user is typing or pasting input; users must explicitly press Search.
* The search area uses a two-option mode selector for Single search and Multi-search.
* The Sites table toolbar shows the current total result count for the active search/filter/multi-search context.
* The filters area uses a command bar with a collapsible full filter panel.
* The filters command bar shows the active advanced-filter count, the current saved filter set state, and primary filter actions.
* The filters command bar shows a compact summary of active advanced filters only when the full filter panel is collapsed.
* Clicking the filter summary area or using the `Show filters` / `Hide filters` action expands or collapses the full filter panel.
* When advanced filters are active, the filters command bar provides a secondary `Clear filters` action. It clears advanced filters only and preserves single-search text, multi-search mode, and multi-search input/results.

Saved filter set rules:

* Saved filter sets are stored per user and per table.
* Saved filter sets store the current advanced filter criteria.
* Users can save at most 40 filter sets per table.
* Saved filter set names must be unique per user/table ignoring case.
* Users can create, apply, update, rename, and delete saved filter sets.
* When no saved filter set is selected, the command bar labels the selector as `Saved sets` if there are no savable current filter criteria and as `Unsaved filters` when the current criteria are not tied to a saved filter set.
* The filter set selector is used for switching between current filters and saved filter sets. It also contains secondary saved-filter-set actions such as save as new, duplicate, rename, and delete.
* Saving a new filter set from an active saved filter set pre-fills the name as `{old name} copy`.
* When an active saved filter set has no unsaved saved-filter criteria changes, the save-as action is labeled `Duplicate filter set`; otherwise it is labeled `Save as new filter set`.
* Applying a saved filter set updates the current filters and resets pagination.
* Domain search is not saved in saved filter sets. Domain search is a separate search UI state and can additionally narrow the current result set.
* Stop list domains are not saved by default. They can be included only when the stop list is currently applied.
* Multi-search mode and pasted Multi-search input are never saved in saved filter sets.
* Creating a saved filter set does not make it active when the current advanced-filter state contains applied stop-list domains that were not included in the saved settings.
* Applying a saved filter set in Single search mode applies the saved stop-list state. If the saved set has no stop-list domains, the current stop list is cleared.
* Applying a saved filter set in Multi-search mode preserves Multi-search mode and the pasted Multi-search input; saved stop-list domains are not applied in that mode.
* Stop-list domains inside one saved filter set support at most 50,000 unique normalized domains, and the saved filter payload must fit the backend payload-size limit.

Stop list rules:

* Stop list accepts domains or URLs and normalizes them before matching.
* Stop list excludes exact normalized domains from normal sites search/filter results and export.
* Stop list filtering happens before pagination, sorting, and total count calculation.
* Duplicate stop-list values after normalization are ignored.
* Invalid stop-list values must reject the request; valid values are not partially applied.
* Stop list supports at most 50,000 unique normalized domains.
* Stop list is not available in Multi-search mode.

Niche filter rules:

* Niche values are split by comma into normalized multi-word tokens.
* Niche include filtering uses multi-select ANY semantics.
* Niche exclude filtering removes sites with any excluded normalized niche token.
* Empty or placeholder niche values such as `N/A`, `NA`, `-`, `None`, and `null` are not filterable values.

Topic fit rules:

* Topic fit includes Niche and Categories filters.
* In Expand mode, when both Niche include and Categories include are active, a site matches if either group matches.
* In Narrow mode, when both Niche include and Categories include are active, a site matches only if both groups match.
* If only one include group is active, Expand and Narrow produce the same include result.
* Niche exclude and Categories exclude always apply as exclusions after the include match.
* Topic fit filters are active filters for the sites table, multi-search found-row filtering, export, and export analytics snapshots.

## Multi-search

Multi-search lets users paste many domains/URLs and see which ones exist in the catalog.

Availability:

* Available to all authenticated users.

Input rules:

* Uses the same search area as normal search, with a Multi-search mode/toggle.
* Input accepts domains or URLs separated by spaces and/or new lines.
* Maximum input count: 5,000.
* Inputs are normalized before matching.
* Matching is exact normalized `Domain` equality, not substring search.
* Duplicate inputs after normalization are detected, removed from search execution, and reported to the user.

Display rules:

* Found rows are shown in the same sites grid as normal search results.
* By default, found and not found rows are shown together in normalized input order after duplicate removal.
* Multi-search uses `All Sites` as the default quarantine filter, consistent with Single search, so found unavailable sites remain visible unless the user explicitly filters them out.
* Not found rows contain only `Domain`; all other columns show placeholders.
* Not found rows should be visually distinguishable from found rows, for example with a subtle warning row background.
* When no filters are active, the Sites table result count shows unique searched domains, found domains, and not found domains.
* Filters apply only to found site rows. When filters are active, not found rows are hidden from the grid and the result count shows visible rows out of unique searched domains plus how many not found domains are hidden by filters.
* Applying a new Multi-search resets table pagination to the first page and updates the pagination count for the new result set.
* Domain/default sorting in Multi-search means normalized input order.
* Sorting by a non-domain column sorts found rows by that column and appends not found rows afterward in normalized input order.
* Internal users can copy values from visible price-column headers in Multi-search. Client-role users must not see or access price-column copy actions.
* Price-column copying uses all unique Multi-search result rows in normalized input order after duplicate removal.
* Price-column copying is not affected by table sorting, filters, or pagination.
* Copied price-column values are newline-separated for spreadsheet paste. Numeric prices are copied as plain numbers without currency symbols or display formatting, `YES` and `NO` are copied as text, and unknown or not-found values are copied as blank lines to preserve row alignment with the pasted Multi-search input.

Export rules:

* Export keeps separate workbook sheets: found rows in the `Sites` sheet and, only when any domains are not found, those domains in a `Not found` sheet.
* With Domain/default sorting, the `Sites` sheet preserves normalized input order after duplicate removal.
* With non-domain sorting, the `Sites` sheet uses the active sort for found rows.
* The `Not found` sheet always preserves normalized input order after duplicate removal.
* Filters apply only to found rows in the `Sites` sheet. Not found domains remain included in the `Not found` sheet.
* Effective export limits apply to found rows.

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
* Import result summaries are kept per user and per import type in browser storage for up to 30 minutes after a successful import.
* Starting a new import clears only the current import type's selected file, errors, and saved result summary.
* Import types have dedicated frontend routes under `/imports`, including `/imports/sites-import` and `/imports/sites-update-import`.
* Import logs must record who ran the import, when it happened, import type, and summary counts.
* Duplicate domains in an input file should have explicit behavior. Current update-style imports use last valid row wins.
* Import results distinguish invalid rows that were not saved from warning rows that were saved with review warnings.
* Warning row downloads contain only `Domain`, `Location`, `Source Row Number`, and `Warning Details`.

### Sites import

Purpose: add new sites to the catalog.

Required base columns:

1. `Domain`
2. `DR`
3. `Traffic`
4. `Location`

Optional base columns:

* `Niche`
* `Categories`
* `NumberDFLinks`
* `SponsoredTag`
* `Language`

Pricing columns are optional flat columns. If any pricing column is present, the file must include a row-level `Term` column. Supported pricing columns are `PriceUsd`, `PriceCasino`, `PriceCrypto`, `PriceLinkInsert`, `PriceLinkInsertCasino`, and `PriceDating`.

`Term` values are empty, `No term`, `permanent`, or positive finite year labels such as `1 year` or `2 years`. Empty `Term` and `No term` are stored with the internal unknown term key and displayed in the UI as `No term`. The legacy `unknown term` text is accepted as an alias but is not shown in UI instructions. A non-empty invalid term, including a cell containing multiple terms, is a row-level error.

Optional service columns accept empty, `YES`, `NO`, or a positive numeric price. Empty means `Unknown`, `YES` means `AvailableWithUnknownPrice`, `NO` means `NotAvailable`, and a numeric price means `Available`.

Rules:

* Add-only import.
* `Language` is part of the required base column order; empty values are stored as empty/null, accepted values are normalized, and invalid values are row-level errors.
* New insert import writes prices to `SitePriceOptions` and optional service statuses to `SiteServiceAvailabilities`; it does not write imported pricing to legacy flat `Site` price fields.
* `PriceUsd` cells are empty or positive numeric values only. Empty creates no price option; `0`, negative values, `YES`/`NO`, and non-numeric values are invalid.
* Service cells are empty, `YES`, `NO`, or positive numeric values only; `0`, negative values, and other non-numeric values are invalid.
* Duplicate pricing columns are invalid.
* If an optional service has a numeric price in the row, its availability is saved as `Available`.
* Existing domains are skipped and reported.
* New domains are inserted.
* Domain is normalized before uniqueness checks.
* Duplicate domains inside the input are reported.
* Invalid rows are not inserted.
* Empty Location values are saved with canonical `UNKNOWN` and do not create warnings.
* Recognized Location values and aliases are saved with their canonical `LocationKey` and the raw imported value preserved.
* Non-empty unrecognized Location values are saved with null `LocationKey` (`Other`) and create a warning row, but are not invalid only because of Location.
* Empty rows are skipped.
* Import should support large catalog files.

### Sites update import

Purpose: mass-update existing sites by domain.

Rules:

* Requires a `Domain` header and at least one supported update column.
* Supports editable non-pricing catalog columns plus row-term pricing columns.
* Column order is flexible.
* Unknown, duplicate, or blank headers are invalid.
* If any pricing column is present, the file must include `Term`; `Term` alone does not count as an update column.
* Supported pricing columns are `PriceUsd`, `PriceCasino`, `PriceCrypto`, `PriceLinkInsert`, `PriceLinkInsertCasino`, and `PriceDating`.
* Old term-in-header pricing columns such as `PriceUsd [1 year]` and old `Price...Availability` columns are invalid.
* `Domain` is the lookup key and must never be changed by the import.
* Updates existing sites only.
* Only columns present in the CSV are updated.
* Missing columns leave existing values unchanged.
* Present empty values are explicit updates and follow field-specific rules.
* Present empty `PriceUsd` cells clear the exact main price option for the row `Term`.
* Present numeric `PriceUsd` cells upsert the exact main price option for the row `Term`.
* Present numeric service cells upsert the exact service price option for the row `Term` and set that service availability to `Available`.
* Present service `YES`, `NO`, or empty cells remove only the exact service price option for the row `Term`.
* After a service `YES`, `NO`, or empty cell, service availability is set to `Available` if any numeric prices for that service remain on other terms; otherwise it is set from the cell value.
* Empty `Language` values overwrite existing language with empty/null.
* Present empty nullable fields clear existing values according to the field storage convention.
* Present empty required fields such as `DR` and `Traffic` are row-level errors.
* If the `Location` column is missing, existing canonical location fields are unchanged.
* If the `Location` column is present, empty values set canonical `UNKNOWN`, recognized values set the mapped canonical `LocationKey`, and non-empty unrecognized values set null `LocationKey` (`Other`) with a warning row.
* Unknown domains are reported as unmatched; they are not inserted.
* Last valid row wins for duplicate domains in the file.
* Invalid rows are reported and not applied.
* Invalid and unmatched row downloads preserve the original uploaded headers.
* Updates must preserve quarantine and last published fields unless explicitly part of the import behavior.

### Availability import

Purpose: update whether existing sites are currently available.

Supported actions:

* `markUnavailable`: mark matched sites as unavailable.
* `restoreAvailable`: restore matched sites as available.

Required columns for `markUnavailable`, in order:

1. `Domain`
2. `Reason`

Required columns for `restoreAvailable`, in order:

1. `Domain`

Rules:

* Updates existing sites only.
* Domain is normalized and matched by exact equality.
* `markUnavailable` matched rows set `IsQuarantined = true`.
* `markUnavailable` stores the optional `Reason` as `QuarantineReason`; an empty reason clears any previous reason.
* `restoreAvailable` matched rows set `IsQuarantined = false` and clear `QuarantineReason`.
* `restoreAvailable` is idempotent; already available sites are processed without error.
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

Exports produce Excel `.xlsx` files from the current catalog context.

Rules:

* Export respects current filters, search, sorting where supported, and multi-search mode.
* Export includes only the Sites table columns currently visible in the UI, including unsaved column visibility/order changes.
* Export column order must match the current Sites table visible column order.
* Export column names should match the Sites UI table where a matching UI column exists.
* Export values should match Sites UI display formatting where practical, except UI-only empty-state placeholders such as `Last Published` `-` and term-aware price cells, which export contextual raw numeric values instead of the UI's multi-term display.
* The backend must validate requested export column keys and reject unknown, blank, UI-only/action, non-exportable, or role-forbidden columns.
* Duplicate requested export column keys are normalized to the first occurrence.
* Client-role users must not be able to export internal-only columns by manually editing export requests.
* Export workbooks should remain editable to the right of exported site columns in spreadsheet tools.
* Export workbooks include a `Sites` sheet and an `Export info` sheet.
* The `Export info` sheet explains term-aware price selection. With no selected term, it states that no term was applied and the minimum available price was selected for each price column. With a selected term, it states the selected term label and that only prices for that term were selected.
* Export workbooks include a `Not found` sheet only for multi-search exports that have not-found domains included by the export rules.
* Export must enforce the user's effective export policy.
* Export actions should be logged.
* Export logs distinguish regular download delivery from Google Drive delivery.
* Client-role exports create a separate analytics snapshot of active filters, sorting, and available search context for future analysis. These snapshots must not store exported site IDs or exported domains.
* If export is truncated by limit, the user must be informed.
* Disabled export must be enforced by backend, not only by hiding the button.
* The Sites export UI offers both `Download Excel` and `Save to Google Drive`.
* On `/sites`, export actions live in the Sites table toolbar so they are tied to the current table view, visible columns, search, filters, and sorting.
* The existing Excel download export must remain available and unchanged when Google Drive is not connected or unavailable.
* Export logs store requested row count, exported row count, truncation state, row-limit value, active client usage-limit values, destination, export mode, and blocked reason when applicable.
* Successful and partially successful exports persist the actual exported site domains linked to the export log so rolling unique-domain usage can be calculated.
* Exported-domain access rows are retained only for rolling usage checks and may be cleaned up after the configured retention period, which must not be shorter than 7 days.
* Client-role analytics snapshots of filters, sorting, and search context remain separate from exported-domain access records.

### Google Drive export connection

Users may optionally connect their own Google Drive account after logging in with their Redhead account. This is separate from authentication and must not be implemented as Google sign-in, Google registration, or external login.

Rules:

* Google Drive connection is optional per user.
* The connection uses the minimal Google Drive OAuth scope: `https://www.googleapis.com/auth/drive.file`.
* The broad `https://www.googleapis.com/auth/drive` scope is out of scope and must not be used.
* Google Drive exports use the same Sites Excel export behavior as regular downloads, including filters, sorting, single search, multi-search, export limits, permissions, export logging, and client analytics snapshots.
* Google Drive exports save files to a dedicated folder in the user's My Drive.
* The dedicated folder name comes from configuration and defaults to `Redhead Catalog Exports`.
* Users do not choose a destination folder in the export UI.
* The frontend should ask users to connect Google Drive before the first Google Drive export and should ask them to reconnect if access expires or is revoked.
* After a successful Google Drive export, the frontend should open the created Google Drive file automatically when the browser allows it and should always provide an `Open file` fallback action.
* Public OAuth verification pages are available without login at `/oauth-home`, `/privacy-policy`, and `/terms-of-service`.
* If the stored export folder id is missing or no longer points to an available folder, the backend should create a dedicated My Drive folder with the configured name before upload.
* Exports must fail clearly instead of silently saving to Drive root when the dedicated folder cannot be ensured.
* Shared Drive support is out of scope.
* Folder picker support is out of scope.

## Admin UI

Admin UI exists to manage users, role export limits, and imports.

Rules:

* Admin navigation should show only sections the current user can access.
* Backend policies remain authoritative even if navigation hides a section.
* User creation UI is available only to `SuperAdmin`.
* The Users page should keep creation out of the table flow by opening the create-user form from an `Add user` dialog.
* User reactivation UI is available only to `SuperAdmin` for disabled users.
* Role-change UI is available only to `SuperAdmin` for active non-`SuperAdmin` users.
* Admin users list should show `DisplayName` for completed profiles and activation/profile status otherwise.
* Admin users list should show the user's name/profile status and email together in a single user-identification column.
* `SuperAdmin` and `Admin` can view readonly admin user details, including account role, display name, activation/profile status, export-limit information, and Google Drive connection status.
* For `Client` users, admin user details also show current rolling export-usage values for the last 24 hours and last 7 days.
* `SuperAdmin` can view and edit the optional internal note in SuperAdmin user management responses.
* Admins must not edit another user's `DisplayName`.
* Admins must not see or edit the internal `SuperAdmin` note.
* Role settings editing is available only to `SuperAdmin`.
* Per-user export override editing is available only to `SuperAdmin`.
* `SuperAdmin` export settings are shown as unlimited and not editable.
* `SuperAdmin` can access an Analytics page for Business Demand based on Client export requests.
* Business Demand analytics aggregate Client export logs and export analytics snapshots server-side. They summarize export request volume, Client activity, requested rows, exported domains, selected filter values, service demand, quality ranges, and export strictness.
* Business Demand price range analytics are based on the `priceUsd` main-price filter stored in export analytics snapshots. Term-aware pricing adds selected term demand and price-range-by-term demand; export logs without `termKey` are counted as `Any term`.
* Business Demand analytics are based on export requests, not all UI searches, and must not expose raw export logs or raw filter/sort/search snapshot JSON in the page.
* `SuperAdmin` can access an Export Activity analytics tab based on Client export logs and exported-domain access records.
* Export Activity analytics summarize completed, partial, and blocked exports; unique exported domains; requested versus exported rows; daily export activity; per-client export results inside the selected period; and paginated recent export logs.
* Export Activity page filters apply to the selected-period summaries, daily activity, per-client export summary table, and recent logs.
* Export Activity recent logs must show readable filter and sort summaries and must not display raw snapshot JSON.
* Export Activity recent logs allow `SuperAdmin` users to open a detail drawer for a selected log. The drawer loads details by log id, shows readable filter, sort, and search context, and keeps raw snapshot JSON collapsed under technical details.

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
