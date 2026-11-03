# Webmaster profile migration plan

Status: **paused by the user on 2026-11-03**, pending business decisions.
Start with the [paused-task handoff](webmaster-profile-migration-handoff.md) when resuming.
The draft below records the earlier membership-based proposal. Subsequent discussion proposed
direct offer-to-profile ownership and canonical contact editing on the profile, with legacy offer
Contact retained only as read-only source evidence. Those decisions are not finalized; the draft
must be reconciled with business answers before implementation. It is not an approved specification.

Audit completed on 2026-10-29; no application profile migration or UI has been implemented.
This document describes the proposed implementation, not current application behavior.
The [preflight record](data-migrations.md#2026-10-29-webmaster-profile-preflight) owns snapshot
counts and verification evidence. Current behavior remains in [business requirements](business-requirements.md).

## Earlier draft: grouping and preservation

* Add `WebmasterProfiles` and `WebmasterProfileMembers` above existing technical `Webmasters`.
  Do not merge/delete source Webmasters or repoint offers to a chosen surviving Webmaster.
* Group by the saved primary contact, trimmed and lowercased for equality. Keep the original
  display value. The public field still uses `PrimaryEmail`; a label rename is separate work.
* Email, non-email text, and an existing unique empty-Contact placeholder are all valid profile
  keys. Do not strip dots/plus suffixes or merge by site domain or secondary Contact addresses.
* Do not reparse or change a populated PrimaryEmail. The earlier cleanup/import rules select
  a marked address, otherwise the first valid email, otherwise trimmed Contact text, otherwise
  a unique placeholder. These rules are not rerun as part of profile membership creation.
* Every source Webmaster has exactly one membership, including identities with no offers.
  Every existing offer consequently belongs to one profile through its source Webmaster.
* Preserve all offers, even when several members have offers for the same domain. Preserve
  offer IDs, fingerprints, prices, statuses, terms, mailboxes, raw Contact, source metadata,
  existing histories, and catalog data.

## Contacts in the first version

No separate persisted Contacts table or combined mutable Contact text column is needed.
Build the profile's contact view from member and offer Contact evidence:

* Show each email once using trimmed/lowercase equality, without address-provider rewrites.
* Preserve distinct annotations and non-email text, URLs, and internal line structure. Identical
  display text can be deduplicated; differing notes must remain available.
* Retain source identity, offer, and domain references. A restriction or condition for one site
  must not become a claim that the address is usable or unusable for every profile domain.
* Keep original Contact fields and their history as the authoritative evidence.

Typed contact CRUD, priorities, verification/status, and a separate Contacts table can be designed
later if needed. Profile creation must not discard evidence while preparing that future option.

## Schema and backfill

1. Add profile and membership entities/configuration. Use foreign keys and a unique membership
   constraint for each Webmaster. Profile display metadata is separate from retained source fields.
2. Support the existing 20,000-character primary-contact limit. Do not put an unrestricted text
   key directly into a unique B-tree index: current short keys do not guarantee future inputs fit.
   Proposed implementation: a unique SHA-256 key plus the full normalized value; compare the full
   value and fail explicitly on a hash collision rather than associating different contacts.
3. Create one profile per normalized saved key and one membership per Webmaster. Choose display
   representatives deterministically without overwriting member DisplayName/Notes.
4. Recheck prerequisites inside the migration transaction. Abort on new empty primary contacts,
   inconsistent grouping, missing references, or incompatible normalization. Do not silently skip
   identities or repair their PrimaryEmail during profile creation.
5. Add `AddedAtUtc` and `AddedBy` to membership records so the backfill's provenance is visible.
   Record profile creation as a data migration. Do not present unchanged offer fields as edits.
6. Preserve old migrations unchanged. Apply the new schema/backfill through normal EF startup.

## Ongoing membership consistency

These rules must be implemented with the schema before the grouped UI is enabled:

* Imports continue matching technical identities by normalized Contact and keep immutable offer
  fingerprints. Attaching that identity to a profile is a separate step using its saved primary.
* A newly created identity joins the existing exact-key profile or creates one.
* Reusing an identity does not re-extract its primary or replace a manual choice.
* A manual primary change moves only the affected technical identity's membership to the
  matching profile; all offers linked to that identity follow it. It does not rewrite every
  member's primary merely because they previously shared a profile.
* Contact relinking keeps its existing offer-level scope. Resolve profile membership for the
  resulting target identity without changing sibling offer/source data.
* Save source changes and membership together. Handle concurrent creation of the same profile
  through database uniqueness and a retry/conflict outcome; do not leave partial memberships.
* Retain empty profiles and offerless identities unless a separate cleanup is explicitly requested.

## Webmasters page

* Search Contact evidence as today, but return one result per matching profile, with counts across
  all its members and offers. Search results must not preload complete profile workspaces.
* Expose an explicit profile ID and member count. Preserve source Webmaster IDs on offers;
  do not silently reuse one identifier name for two different entity types.
* The selected workspace includes all profile members' domains/offers and the derived contact view.
  Other-webmaster counts must exclude the entire selected profile, not just one source member.
* Paginate/lazily load workspace domains/offers server-side. The audited largest groups make an
  unbounded eager-loaded response and rendering every offer unsuitable as the default.
* Reuse current Webmasters read authorization. Changing profile/offer lifecycle, prices, or
  permissions is outside this migration. Define compatibility for existing identity-based URLs.

## Verification and rollout

* Test exact-key membership, text/placeholder keys, case/whitespace normalization, source
  preservation, repeated domains, contact deduplication with annotations, and offerless identities.
* Test import/reimport, manual primary changes, Contact relinking, optimistic concurrency,
  profile-creation races, and transactional rollback on PostgreSQL.
* Test search/counts, profile-level other-offer counts, pagination, and authorization. Follow the
  repository rule against adding frontend unit tests without an explicit request; use existing
  checks and focused browser verification.
* Restore the pristine snapshot into a new rehearsal database and apply the actual EF migration.
  Compare row checksums and stable identifiers, verify full membership and repeated-run safety,
  measure representative large-profile requests, and rehearse guarded rollback.
* Take a fresh production backup and pause offer imports/edits for rollout. Verify membership
  totals and zero unprofiled offers against the current production state, not old fixed counts.
* Rollback may remove the new grouping only while no later profile/membership edits would be lost.
  Abort on intervening changes; reverting an application image alone does not undo database changes.

Implementation sequence: schema/backfill and provenance; runtime synchronization; grouped,
paged read API/UI and contact view; full local rehearsal and deployment verification.
