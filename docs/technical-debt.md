# Technical debt

This file records deferred engineering work and instructions for maintaining the register.
Entries do not change current product behavior; business rules remain in
`docs/business-requirements.md`.

## Recording and maintaining debt

* Add an entry when work is explicitly deferred and the current limitation should remain
  visible. Check existing entries first to avoid duplicates.
* Give each entry a stable sequential identifier (`TD-001`, `TD-002`, etc.) and a short title.
  Do not renumber or reuse identifiers.
* Record its status, the current limitation and impact, the reason for deferral, affected
  components, and the work and verification needed to resolve it. Leave undecided settings
  explicit instead of inventing retention periods or other product decisions.
* Keep implementation details here. Link a nearby code `TODO(TD-...)` to this file when useful.
  A code comment alone is not the debt record.
* Use `deferred`, `in progress`, or `resolved` as the status. Mark an entry resolved only after
  the work and relevant verification are complete; retain the entry and record the resolution.
* When resolving an entry, update business requirements or deployment documentation alongside
  the implementation if behavior or operations change.

## TD-001: LinkDetective sync history retention

Status: deferred. Automatic history cleanup is not required for the current delivery.

Affected components: `LinkDetectiveSyncRuns`, `LinkDetectiveSyncRunItems`, and the
background-sync monitoring and restart-recovery workflows.

`LinkDetectiveSyncRuns` and `LinkDetectiveSyncRunItems` currently have no automatic
cleanup. Each selection adds per-domain work records, so completed history grows with
the number of selected domains across runs. Active run items are also the durable work
queue used for retries and restart recovery. They contain diagnostics, not price snapshots;
current market prices are stored separately in `LinkDetectivePriceCaches`.

Future work:

* Agree retention periods for completed run summaries and per-domain details before
  implementing cleanup; no duration has been chosen yet.
* Clean eligible completed history in bounded batches, using the run's completion time.
  Preserve all `Running` and `Waiting` runs and their items regardless of age.
* When removing a completed run, remove its items through the existing cascade relationship.
* Preserve the shared price cache and `LinkDetectiveSyncStates`. History cleanup must not
  reset cache freshness, consume provider quota, or disrupt pending work after a restart.

Implementation should verify retention boundaries, preservation of active work, and
unchanged cached prices and scheduler state.

## TD-002: Shared background-job scheduling

Status: deferred. Architecture proposal recorded; implementation and package/version
selection remain pending a pilot. The migration is deferred so scheduling and recovery
behavior can be verified before replacing the existing workers.

Affected components: the seven workers under `src/Redhead.SitesCatalog.Api/BackgroundJobs`
(LinkDetective sync, Ahrefs sync, weekly emergency export, exported-domain access cleanup,
client activity cleanup, client alerts, and automatic bans), their registration, Cronos
schedule helpers, and execution locks.

Workers repeat scheduling loops, delays, DI scope creation, cancellation handling, and
failure handling. Adding another job requires more infrastructure code, and execution
policies are difficult to review consistently across jobs.

Proposed direction:

* Pilot Quartz.NET as the shared scheduler, using the existing PostgreSQL for persistent
  scheduling state. Account for its additional tables and deployment requirements.
* Keep job adapters small: invoke an application service and let the scheduler manage
  triggers and waiting. Keep domain selection, quotas, durable batch progress, and
  result persistence in the application services.
* Use scheduler overlap protection for the same registered job where appropriate.
  It does not coordinate direct on-demand requests with background work, or guarantee
  exactly-once execution of a paid provider operation. Review existing locks individually.
* Review overlap between scheduler execution records and application run history before
  removing tables. Per-domain progress and diagnostics still have an application purpose;
  LinkDetective history retention remains a separate task in TD-001.
* Consider Hangfire instead if queue management and its ready-made dashboard become the
  main requirement. Its recurring scheduler checks jobs on a minute-based interval;
  delayed and queued jobs have different scheduling behavior. Select one scheduler.
* Avoid introducing a custom universal job framework or base-class hierarchy around the
  chosen library. A new ordinary job should need an execution handler and registration.

Migration and verification:

1. Inventory schedules, time zones, enabled/disabled defaults, startup behavior, overlap
   rules, failure policies, and restart recovery for each worker.
2. Move one simple cleanup worker first and verify that it removes duplicated mechanics
   without changing business behavior. Then migrate the remaining workers incrementally.
3. Verify UTC boundaries, missed triggers, overlapping triggers, shutdown cancellation,
   restart recovery, and interaction with manual operations. Translate and test existing
   Cronos expressions; their standard five-field format must not be copied unchanged to
   Quartz's six/seven-field format.
4. Verify that failures and recovery do not add unintended paid requests, bypass quotas,
   lose pending domains, change authorization, or re-enable inactive Ahrefs sync.
5. Remove replaced loops and schedule helpers. Update deployment/setup documentation for
   scheduler tables and configuration, and product requirements only if behavior changes.

References: [Quartz job stores](https://www.quartz-scheduler.net/documentation/quartz-4.x/tutorial/job-stores.html),
[Quartz concurrency](https://www.quartz-scheduler.net/documentation/quartz-4.x/tutorial/more-about-jobs.html),
[Quartz cron format](https://www.quartz-scheduler.net/documentation/quartz-3.x/tutorial/crontriggers.html),
[Hangfire recurring jobs](https://docs.hangfire.io/en/latest/background-methods/performing-recurrent-tasks.html).

## TD-003: Consistent API clients and outbound-request policies

Status: deferred. Architecture proposal recorded; client-library and policy choices need
a pilot. The migration is deferred until paid-call behavior and tolerant batch parsing
have been verified with controlled responses.

Affected components: LinkDetective and Ahrefs API clients, Google Drive integration,
HTTP-client registration, response parsers, retry loops, and
`LinkDetectiveRequestCoordinator`.

Client creation, authorization, HTTP requests, JSON parsing, and failure handling use
different approaches across integrations. Ahrefs has its own HTTP retry loop;
LinkDetective manually combines semaphores, request timing, and provider cooldowns.
New integrations risk copying these mechanics and applying inconsistent policies.

Proposed direction:

* Standardize registration through `IHttpClientFactory`. Evaluate Refit for declarative
  HTTP contracts and ordinary DTO deserialization. Prefer a suitable official provider
  SDK where available; Google Drive already has one in the repository.
* Keep per-provider contracts, response DTOs, and semantic validation explicit. Use
  System.Text.Json for routine deserialization, retaining selective raw-row parsing where
  necessary to preserve valid rows from a partially malformed LinkDetective batch.
* Evaluate `Microsoft.Extensions.Http.Resilience` (Polly-based) for timeouts, backoff,
  retries, and circuit breaking. Define policies per provider and operation. Standard
  handler defaults retry all HTTP methods; paid reads can also consume quota repeatedly.
  Do not enable automatic retries or parallel hedging for paid operations without an
  explicit policy. Coordinate HTTP retries with job-level attempts to avoid multiplying
  provider calls.
* Use `System.Threading.RateLimiting` for outbound frequency/concurrency control, shared
  by background and on-demand traffic for the same provider account/API key. Keep limits
  configurable and evidence-based; do not invent provider maximums or assume a standard
  limiter exactly preserves the existing minimum-interval behavior.
* Separate HTTP admission from consistency protection around cache/progress updates.
  Preserve provider-wide cooldowns and quota accounting. A process-local limiter does
  not enforce a shared limit across application instances; scheduler overlap protection
  does not cover direct HTTP requests either.
* Reuse transport mechanics through small handlers and registration helpers. Keep
  provider-specific errors and validation readable rather than introducing a large
  generic API-client/parser hierarchy.

Migration and verification:

1. Inventory each operation's authentication, timeout, charging, retry safety, quota,
   cooldown, response-validation, and cancellation requirements.
2. Pilot one client and its policies with fake HTTP responses before migrating others.
   Coordinate background/on-demand behavior with TD-002 while keeping each change small.
3. Verify shared request limits, cancellation while queued, provider cooldowns, configured
   retries, and the number of actual outbound attempts. Avoid paid live calls by default.
4. Preserve LinkDetective distinctions between confirmed prices, confirmed not-found,
   and unresolved domains. Verify malformed/partial batches, duplicate results, quota
   responses, preservation of cached successful data, and shared manual/background use.
5. Remove replaced request-building, retry, and timing code. Keep existing business tests
   and add focused coverage for changed policies. Update setup/deployment documentation
   when configuration changes. A new ordinary API client should need a contract, DTOs,
   provider-specific validation, and registration without copying transport mechanics.

References: [Refit](https://github.com/reactiveui/refit),
[IHttpClientFactory](https://learn.microsoft.com/en-us/dotnet/core/extensions/httpclient-factory),
[HTTP resilience](https://learn.microsoft.com/en-us/dotnet/core/resilience/http-resilience),
[outbound rate limiting](https://learn.microsoft.com/en-us/dotnet/core/extensions/http-ratelimiter).

## TD-004: One shared PriceType

Status: deferred. Record the simplification for a separate change; no enum or data migration
is part of this documentation update.

Affected components: domain price enums and entities, EF Core persistence, pricing and
webmaster-offer services, API/frontend contracts, imports, exports, validation, and tests.

`PriceType` and `WebmasterOfferPriceType` describe overlapping services with different numeric
values and some different names. Keeping both requires `CatalogPriceMapping`, duplicated type
definitions, and mapping-specific tests without adding useful business separation.

Planned work:

* Use a single shared `PriceType` for catalog and webmaster-offer prices. Remove
  `WebmasterOfferPriceType`, `CatalogPriceMapping`, and the redundant conversion code and tests.
* Preserve all existing services, including offer-only Banner and Homepage Text Link variants.
  Keep the catalog's currently supported service set explicit; sharing the enum must not
  automatically add catalog price fields or margin comparisons for these extra services.
* Migrate stored offer values explicitly rather than casting or renaming the enum: Dating,
  LinkInsertion, and the LinkInsertionCasino/LinkInsertion18Plus equivalent currently use
  different values. Update backend and frontend contracts together and preserve service meaning
  across existing data, import/export formats, and displayed labels.

Verify migration and rollback against existing numeric values, all supported services, and
unchanged prices, availability, term matching, margins, imports, and exports. Replace mapping-only
tests with coverage of observable behavior using the shared enum.
