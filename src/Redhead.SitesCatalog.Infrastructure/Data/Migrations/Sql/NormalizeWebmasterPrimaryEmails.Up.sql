-- Frozen repair rules for 20261012153852. No production contacts, addresses or IDs are embedded.
-- EF executes this whole script in the migration transaction. Reads remain available during repair.
LOCK TABLE "Webmasters", "SiteWebmasterOffers", "EntityChangeHistories" IN SHARE ROW EXCLUSIVE MODE;
CREATE TEMP TABLE primary_email_repairs_20261012 (
    "Id" uuid, "ContactRawText" text, old_email text, expected_version text, new_email text, rule text
) ON COMMIT DROP;

CREATE OR REPLACE FUNCTION pg_temp.primary_email_matches_v1(input text, pattern text)
RETURNS TABLE(start_pos integer, end_pos integer, value text)
LANGUAGE plpgsql AS $body$
DECLARE cursor_pos integer := 1;
BEGIN
    LOOP
        start_pos := regexp_instr(input, pattern, cursor_pos, 1, 0, 'i');
        EXIT WHEN start_pos = 0;
        end_pos := regexp_instr(input, pattern, cursor_pos, 1, 1, 'i');
        value := substring(input FROM start_pos FOR end_pos - start_pos);
        RETURN NEXT;
        cursor_pos := end_pos;
    END LOOP;
END;
$body$;

CREATE OR REPLACE FUNCTION pg_temp.primary_email_legacy_v1(contact text)
RETURNS TABLE(primary_email text, conflicting_markers boolean)
LANGUAGE plpgsql AS $body$
DECLARE
    email_pattern text := '[A-Z0-9._%+\-]+@[A-Z0-9.\-]+\.[A-Z]{2,}';
    reply_pattern text := '(?<![[:alnum:]])(?:ответили здесь|отвечают здесь|ответили тут|отвечают тут|ответил тут|ответ здесь|ответ туту|ответы тут|отвечает тут|ответт тут|ответ тту|ответ утт|primary email|ответ тут|отв тут|otv tut|primary|ответ|answer|reply|отв|otv)(?![[:alnum:]])';
    direction_pattern text := '(?<![[:alnum:]])(?:направили сюда|написала сюда|написать сюда|писать сюда|write here|здесь|сюда|here|тут)(?![[:alnum:]])';
    marker_pattern text;
    line text;
    email_match record;
    marked_emails text[];
    all_emails text[];
BEGIN
    primary_email := NULL;
    conflicting_markers := false;
    FOREACH marker_pattern IN ARRAY ARRAY[reply_pattern, direction_pattern] LOOP
        marked_emails := ARRAY[]::text[];
        FOREACH line IN ARRAY regexp_split_to_array(contact, E'[\r\n]+') LOOP
            IF line ~* marker_pattern THEN
                FOR email_match IN SELECT * FROM pg_temp.primary_email_matches_v1(line, email_pattern) LOOP
                    IF primary_email IS NULL THEN primary_email := lower(email_match.value); END IF;
                    IF NOT lower(email_match.value) = ANY(marked_emails) THEN
                        marked_emails := array_append(marked_emails, lower(email_match.value));
                    END IF;
                END LOOP;
            END IF;
        END LOOP;
        IF primary_email IS NOT NULL THEN
            conflicting_markers := cardinality(marked_emails) > 1;
            RETURN NEXT;
            RETURN;
        END IF;
    END LOOP;
    SELECT array_agg(lower(value)) INTO all_emails FROM pg_temp.primary_email_matches_v1(contact, email_pattern);
    IF cardinality(all_emails) = 1 THEN primary_email := all_emails[1]; END IF;
    RETURN NEXT;
END;
$body$;

CREATE OR REPLACE FUNCTION pg_temp.primary_email_unambiguous_v1(contact text, saved_primary text)
RETURNS TABLE(proposed_email text, rule text)
LANGUAGE plpgsql AS $body$
DECLARE
    email_pattern text := '[A-Z0-9._%+\-]+@[A-Z0-9.\-]+\.[A-Z]{2,}';
    reply_pattern text := '(?<![[:alnum:]])(?:ответили здесь|отвечают здесь|ответили тут|отвечают тут|ответил тут|ответ здесь|ответ туту|ответы тут|отвечает тут|ответт тут|ответ тту|ответ утт|primary email|главный контакт|основной контакт|основная почта|основной email|ответ тут|отв тут|otv tut|отвечают|отвечает|ответили|ответила|ответил|primary|ответ|answer|reply|отв|otv)(?![[:alnum:]])';
    direction_pattern text := '(?<![[:alnum:]])(?:направили сюда|написала сюда|написать сюда|писать сюда|write here|здесь|сюда|here|тут)(?![[:alnum:]])';
    restriction_pattern text := 'не\s+(пишем|писать|пиши|отвеч|работает|использ|актуал)|устарел|неактуал|недейств|do\s+not\s+(contact|write|use)|don''t\s+(contact|write|use)|no\s+(reply|answer)|not\s+working';
    emails text[];
    marker record;
    marker_count integer;
    marker_start integer;
    marker_end integer;
    marker_value text;
    before_email record;
    after_email record;
    prefix text;
    suffix text;
    gap_before text;
    gap_after text;
    before_adjacent boolean;
    after_adjacent boolean;
BEGIN
    IF contact ~* restriction_pattern THEN RETURN; END IF;
    SELECT array_agg(DISTINCT lower(value)) INTO emails FROM pg_temp.primary_email_matches_v1(contact, email_pattern);
    IF cardinality(emails) = 1 AND saved_primary IS NULL THEN
        proposed_email := emails[1];
        rule := 'Single distinct repeated email';
    ELSE
        marker_count := 0;
        FOR marker IN SELECT * FROM pg_temp.primary_email_matches_v1(contact, reply_pattern) LOOP
            IF NOT EXISTS (SELECT 1 FROM pg_temp.primary_email_matches_v1(contact, email_pattern) e
                           WHERE marker.start_pos >= e.start_pos AND marker.start_pos < e.end_pos) THEN
                marker_count := marker_count + 1;
                marker_start := marker.start_pos;
                marker_end := marker.end_pos;
                marker_value := lower(marker.value);
            END IF;
        END LOOP;
        IF marker_count = 0 THEN
            FOR marker IN SELECT * FROM pg_temp.primary_email_matches_v1(contact, direction_pattern) LOOP
                IF NOT EXISTS (SELECT 1 FROM pg_temp.primary_email_matches_v1(contact, email_pattern) e
                               WHERE marker.start_pos >= e.start_pos AND marker.start_pos < e.end_pos) THEN
                    marker_count := marker_count + 1;
                    marker_start := marker.start_pos;
                    marker_end := marker.end_pos;
                    marker_value := lower(marker.value);
                END IF;
            END LOOP;
            IF marker_value IS NULL OR marker_value NOT IN ('писать сюда', 'написать сюда', 'написала сюда', 'направили сюда', 'write here') THEN RETURN; END IF;
        END IF;
        IF marker_count <> 1 THEN RETURN; END IF;
        -- These are the parts of the SAME original source line surrounding the marker.
        prefix := regexp_replace(left(contact, marker_start - 1), E'^.*[\r\n]', '', 's');
        suffix := regexp_replace(substring(contact FROM marker_end), E'[\r\n].*$', '', 's');
        SELECT * INTO before_email FROM pg_temp.primary_email_matches_v1(prefix, email_pattern) ORDER BY start_pos DESC LIMIT 1;
        SELECT * INTO after_email FROM pg_temp.primary_email_matches_v1(suffix, email_pattern) ORDER BY start_pos LIMIT 1;
        gap_before := CASE WHEN before_email.value IS NOT NULL THEN substring(prefix FROM before_email.end_pos) END;
        gap_after := CASE WHEN after_email.value IS NOT NULL THEN left(suffix, after_email.start_pos - 1) END;
        before_adjacent := coalesce(gap_before ~ '^[ \t=:(\[\-–—]*$', false);
        after_adjacent := coalesce(gap_after ~ '^[ \t=:()\[\]\-–—]*$', false);
        IF before_adjacent AND after_adjacent THEN
            IF gap_before ~ '[\(\[]' AND gap_after ~ '^[ \t]*[\)\]]' THEN proposed_email := lower(before_email.value); END IF;
        ELSIF before_adjacent THEN proposed_email := lower(before_email.value);
        ELSIF after_adjacent THEN proposed_email := lower(after_email.value);
        END IF;
        rule := CASE WHEN saved_primary IS NULL THEN 'Explicit main/reply marker' ELSE 'Correct marked email' END;
    END IF;
    IF proposed_email IS NOT NULL AND length(proposed_email) <= 320
       AND proposed_email ~ '^[a-z0-9_%+\-]+(?:\.[a-z0-9_%+\-]+)*@[a-z0-9](?:[a-z0-9\-]*[a-z0-9])?(?:\.[a-z0-9](?:[a-z0-9\-]*[a-z0-9])?)*\.[a-z]{2,}$'
    THEN RETURN NEXT;
    END IF;
END;
$body$;

INSERT INTO primary_email_repairs_20261012
SELECT w."Id", w."ContactRawText", w."PrimaryEmail" AS old_email, w.xmin::text AS expected_version,
       repair.proposed_email AS new_email, repair.rule
FROM "Webmasters" w
CROSS JOIN LATERAL pg_temp.primary_email_legacy_v1(w."ContactRawText") legacy
CROSS JOIN LATERAL pg_temp.primary_email_unambiguous_v1(w."ContactRawText", nullif(lower(btrim(w."PrimaryEmail")), '')) repair
WHERE nullif(lower(btrim(w."PrimaryEmail")), '') IS NOT DISTINCT FROM legacy.primary_email
  AND (nullif(btrim(w."PrimaryEmail"), '') IS NULL OR legacy.conflicting_markers)
  AND repair.proposed_email IS DISTINCT FROM nullif(lower(btrim(w."PrimaryEmail")), '')
  AND NOT EXISTS (
      SELECT 1 FROM "SiteWebmasterOffers" o JOIN "EntityChangeHistories" h
        ON h."EntityType" = 'SiteWebmasterOffer' AND h."EntityId" = o."Id"::text AND h."Source" = 'Manual'
      WHERE o."WebmasterId" = w."Id" AND (
          jsonb_typeof(h."ChangesJson") IS DISTINCT FROM 'array' OR EXISTS (
              SELECT 1 FROM jsonb_array_elements(CASE WHEN jsonb_typeof(h."ChangesJson") = 'array' THEN h."ChangesJson" ELSE '[]'::jsonb END) change
              WHERE lower(change->>'field') IN ('contact', 'primary email', 'contactrawtext', 'primaryemail'))));

-- One authoritative Webmaster record and a visible mirror on every affected offer.
INSERT INTO "EntityChangeHistories" ("Id", "EntityType", "EntityId", "Action", "Source", "ChangedBy", "ChangedAtUtc", "ChangesJson")
SELECT gen_random_uuid(), entity_type, entity_id, 'Updated', 'Data migration',
       'Migration:20261012153852_NormalizeWebmasterPrimaryEmails', transaction_timestamp(),
       jsonb_build_array(jsonb_build_object('field', 'Primary email', 'oldValue', r.old_email, 'newValue', r.new_email),
                         jsonb_build_object('field', 'Cleanup rule', 'oldValue', NULL, 'newValue', r.rule))
FROM primary_email_repairs_20261012 r CROSS JOIN LATERAL (
    SELECT 'Webmaster'::text AS entity_type, r."Id"::text AS entity_id
    UNION ALL SELECT 'SiteWebmasterOffer', o."Id"::text FROM "SiteWebmasterOffers" o WHERE o."WebmasterId" = r."Id"
) entities;

DO $body$
DECLARE affected integer;
BEGIN
    UPDATE "Webmasters" w SET "PrimaryEmail" = r.new_email, "UpdatedAtUtc" = transaction_timestamp()
    FROM primary_email_repairs_20261012 r
    WHERE w."Id" = r."Id" AND w."ContactRawText" = r."ContactRawText"
      AND w."PrimaryEmail" IS NOT DISTINCT FROM r.old_email AND w.xmin::text = r.expected_version;
    GET DIAGNOSTICS affected = ROW_COUNT;
    IF affected <> (SELECT count(*) FROM primary_email_repairs_20261012) THEN
        RAISE EXCEPTION 'Primary email cleanup encountered changed rows; no repairs or history were committed.';
    END IF;
    RAISE NOTICE 'Primary email cleanup: % Webmaster identities updated.', affected;
END;
$body$;

DROP FUNCTION pg_temp.primary_email_unambiguous_v1(text, text);
DROP FUNCTION pg_temp.primary_email_legacy_v1(text);
DROP FUNCTION pg_temp.primary_email_matches_v1(text, text);
