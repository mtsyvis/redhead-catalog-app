-- Guarded rollback keeps both forward and rollback audit evidence; it never overwrites later edits.
LOCK TABLE "Webmasters", "SiteWebmasterOffers", "EntityChangeHistories" IN SHARE ROW EXCLUSIVE MODE;
CREATE TEMP TABLE primary_email_undo_20261012 ON COMMIT DROP AS
SELECT DISTINCT ON (h."EntityId") h."EntityId"::uuid AS webmaster_id, h."ChangedAtUtc" AS applied_at,
       h."ChangesJson"->0->>'oldValue' AS old_email, h."ChangesJson"->0->>'newValue' AS new_email
FROM "EntityChangeHistories" h
WHERE h."EntityType" = 'Webmaster' AND h."Source" = 'Data migration'
  AND h."ChangedBy" = 'Migration:20261012153852_NormalizeWebmasterPrimaryEmails'
ORDER BY h."EntityId", h."ChangedAtUtc" DESC, h."Id" DESC;

DO $body$
BEGIN
    IF EXISTS (
        SELECT 1 FROM primary_email_undo_20261012 r LEFT JOIN "Webmasters" w ON w."Id" = r.webmaster_id
        WHERE w."Id" IS NULL OR w."PrimaryEmail" IS DISTINCT FROM r.new_email OR w."UpdatedAtUtc" IS DISTINCT FROM r.applied_at
          OR EXISTS (
              SELECT 1 FROM "SiteWebmasterOffers" o JOIN "EntityChangeHistories" h
                ON h."EntityType" = 'SiteWebmasterOffer' AND h."EntityId" = o."Id"::text
              WHERE o."WebmasterId" = r.webmaster_id AND h."Source" = 'Manual' AND h."ChangedAtUtc" >= r.applied_at
                AND (jsonb_typeof(h."ChangesJson") IS DISTINCT FROM 'array' OR EXISTS (
                    SELECT 1 FROM jsonb_array_elements(CASE WHEN jsonb_typeof(h."ChangesJson") = 'array' THEN h."ChangesJson" ELSE '[]'::jsonb END) change
                    WHERE lower(change->>'field') IN ('contact', 'primary email', 'contactrawtext', 'primaryemail')))))
    THEN RAISE EXCEPTION 'Cannot roll back primary email cleanup after subsequent edits; use a reviewed correction or restore a backup.';
    END IF;
END;
$body$;

INSERT INTO "EntityChangeHistories" ("Id", "EntityType", "EntityId", "Action", "Source", "ChangedBy", "ChangedAtUtc", "ChangesJson")
SELECT gen_random_uuid(), entity_type, entity_id, 'Updated', 'Data migration',
       'Rollback:20261012153852_NormalizeWebmasterPrimaryEmails', transaction_timestamp(),
       jsonb_build_array(jsonb_build_object('field', 'Primary email', 'oldValue', r.new_email, 'newValue', r.old_email),
                         jsonb_build_object('field', 'Cleanup rule', 'oldValue', NULL, 'newValue', 'Rollback primary email cleanup'))
FROM primary_email_undo_20261012 r CROSS JOIN LATERAL (
    SELECT 'Webmaster'::text AS entity_type, r.webmaster_id::text AS entity_id
    UNION ALL SELECT 'SiteWebmasterOffer', o."Id"::text FROM "SiteWebmasterOffers" o WHERE o."WebmasterId" = r.webmaster_id
) entities;

UPDATE "Webmasters" w SET "PrimaryEmail" = r.old_email, "UpdatedAtUtc" = transaction_timestamp()
FROM primary_email_undo_20261012 r WHERE w."Id" = r.webmaster_id;
