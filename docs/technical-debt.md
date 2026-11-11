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

## TD-005: Upstream container image security findings

Status: in progress. Application package remediation is complete; the upstream-image findings
below remain open. This record is not acceptance of the remaining risk or proof of exploitability.

Affected components: frontend build tooling bundled with npm, `postgres:16-alpine`, and
`caddy:2-alpine`. The app Dockerfile upgrades Alpine packages in all stages and pins npm 11.21.2.
The database and proxy still run unmodified upstream images.

Production follow-up on 2026-11-10:

* Application security commit `fc40875` was deployed; the owner confirmed the application works.
* Caddy was updated from 2.11.2 to 2.11.9 after validating the existing configuration. The external
  HTTPS health check succeeded. The deployed images have not been rescanned on the VPS.
* PostgreSQL remains on 16.13. The owner explicitly deferred its update; no maintenance date is
  set. PostgreSQL image updates and container replacement are excluded from the current work
  unless the owner changes this instruction. The database findings remain open.
* Host versions: Ubuntu 22.04.5 LTS, Docker Engine 29.5.3 and Compose 5.1.4. APT simulation showed
  41 upgrades, four new kernel packages and no removals. About 200 MB of packages were downloaded
  to the VPS package cache only; no package installation or reboot was performed in this session.
* The owner explicitly postponed host maintenance, including installation and reboot. Resume
  those actions only after a new instruction. A fresh maintenance backup was not created.
  Docker daemon or host restarts may interrupt containers without changing PostgreSQL's version.
* Read-only SSH checks found Swarm inactive and Docker API access through a local Unix socket,
  with no dockerd/containerd/BuildKit TCP listeners detected. This is not a full reachability audit.
  Docker 29.5.3 is still in the affected range for the registry DNS/TLS issue
  [GHSA-7cfq-22r6-qp73](https://github.com/moby/moby/security/advisories/GHSA-7cfq-22r6-qp73),
  fixed in 29.8.2; local API access alone does not mitigate that image-pull risk.

Verification on 2026-11-09, Linux amd64, Trivy 0.76.2 with a freshly downloaded vulnerability DB:

| Artifact | Remaining scanner findings | Details |
| --- | ---: | --- |
| App runtime image after remediation | 0 | ASP.NET Core/.NET 10.0.14; Alpine zlib updated to 1.4-r0 |
| Frontend build stage after remediation | 2 medium | npm 11.21.2 bundles glob 13.0.6 and minimatch 10.2.5; CVE-2026-118773 and CVE-2026-118764 |
| PostgreSQL 16.15, freshly pulled `16-alpine` | 62 | One zlib finding and 61 findings in the bundled gosu 1.19 binary, including Go toolchain/module matches |
| Caddy 2.11.9, freshly pulled `2-alpine` | 7 | zlib, go-jose/v3, golang.org/x/crypto and golang.org/x/net |

Direct and transitive NuGet audits of the solution and analytics load tool, and npm audits of
all project dependencies (including dev dependencies), reported no known vulnerabilities.
These results describe checked images and registry data on that date, not the VPS or future builds.

Upstream image indexes checked:

* PostgreSQL: `sha256:aa6cb33cf4c19c765cefcf48d96176258800870a9a1e930ad126200f9d825139`.
* Caddy: `sha256:78bd35025c7ba85961dd149ae670315129954e77abe2383ac47c89106b35ade9`.

Why this remains open:

* Refreshing the current official tags did not remove these findings. Some fixes are available
  in individual libraries but are not incorporated in those images or npm's bundled dependency
  ranges. A patched go-jose/v3 release is not available for
  [CVE-2026-116587](https://github.com/go-jose/go-jose/security/advisories/GHSA-mgcc-cww5-fp2q).
* Gosu [1.20](https://github.com/tianon/gosu/releases/tag/1.20) fixes its moby/sys/user dependency,
  but the checked PostgreSQL image still contains 1.19. Gosu's
  [security policy](https://github.com/tianon/gosu/blob/1.20/SECURITY.md) requires checking function
  reachability before treating Go standard-library version matches as exploitable vulnerabilities.
  Those matches were not individually dismissed in this audit.

Required follow-up:

* Recheck supported npm and image releases and rescan the actual resulting images.
* Assess reachability for the remaining Go findings using vendor guidance and `govulncheck`;
  retain the evidence and any explicit risk decision. Do not suppress findings solely to reach zero.
* If upstream images continue to lag available zlib/gosu fixes, evaluate minimal derived images
  with reproducible, verified inputs. Validate PostgreSQL initialization, restart with existing
  data, helper privilege dropping, and backup/restore; keep major version 16 and persistent volumes.
* For Caddy, verify reverse proxying, HTTPS issuance/renewal and existing persistent state after
  any dependency/image change. Do not silently replace upstream Go dependency majors.
* Scan the deployed VPS images and maintain its OS/Docker separately using
  [the deployment runbook](deployment.md#security-maintenance-and-version-checks).

## TD-006: Simplify and unify optimistic concurrency handling

Status: deferred. Consolidate handling in a separate focused change after reviewing the
existing save paths together; the current quarantine feature keeps its local handling.

Affected components: `GlobalExceptionHandler`, Sites and quarantine-import services,
webmaster-offer editing, catalog pricing, conflict exceptions/results, and their tests.

Concurrency handling currently mixes local catches, entity-specific exception filters,
dedicated exceptions such as `QuarantineConcurrencyException`, and conflict result objects.
This duplicates error translation and makes ordinary saves harder to follow. The application
is small, with only a couple of people editing data; the implementation should reflect that
low contention instead of accumulating infrastructure for each feature.

Planned work:

* Prefer a small shared API mapping of optimistic concurrency failures to `409 Conflict`,
  with a consistent response and a clear instruction to refresh data and retry. A centralized
  `DbUpdateConcurrencyException` mapping is the preferred starting point.
* Remove feature-specific wrappers and catches that only duplicate this translation.
  Keep local handling where it performs necessary rollback, tracked-state cleanup, or
  operation-specific business work. Review existing conflict result contracts together.
* Preserve database version checks and atomic saves. Keep known unique-constraint conflicts
  explicit; unrelated database failures must not be classified as concurrency conflicts.
* Use a refresh-and-retry workflow for rare conflicts. Avoid automatic retries that overwrite
  another editor's changes, new locking infrastructure, or a generic concurrency framework.

Verify successful saves, stale-version conflicts, consistent HTTP responses, and rollback of
all losing-request entity, price, mailbox, import-log, and history changes. Confirm independent
edits still succeed and unrelated database errors retain their existing handling. Update the
affected tests and business requirements with the implementation.
