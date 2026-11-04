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
