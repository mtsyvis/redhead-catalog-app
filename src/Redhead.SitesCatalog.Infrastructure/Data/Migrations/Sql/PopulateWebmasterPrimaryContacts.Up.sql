-- Frozen primary-contact rules for 20261014102750_PopulateWebmasterPrimaryContacts. No production records are embedded.
-- EF executes this whole script in the migration transaction. Reads remain available during repair.
LOCK TABLE "Webmasters", "SiteWebmasterOffers", "EntityChangeHistories" IN SHARE ROW EXCLUSIVE MODE;
CREATE TEMP TABLE primary_email_repairs_20261014 (
    "Id" uuid, "ContactRawText" text, old_email text, expected_version text, new_email text, rule text
) ON COMMIT DROP;

CREATE OR REPLACE FUNCTION pg_temp.primary_email_matches_v2(input text, pattern text)
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

CREATE OR REPLACE FUNCTION pg_temp.primary_email_unambiguous_v2(contact text, saved_primary text)
RETURNS TABLE(proposed_email text, rule text)
LANGUAGE plpgsql AS $body$
DECLARE
    email_pattern text := '[A-Z0-9._%+\-]+@[A-Z0-9.\-]+\.[A-Z]{2,}';
    reply_pattern text := '(?<![[:alnum:]])(?:более актуальная цена|более актуальаня цена|более актульная цена|для быстрого ответа|for communication|основной контакт|главный контакт|ответили отсюда|отвечали с этой|актуальная цена|ответили здесь|отвечают здесь|основная почта|основной email|отвтили отсюда|второй дешевле|primary email|primare email|новый контакт|ответили тут|отвечают тут|отвечает тут|пишет отсюда|ответил тут|ответ здесь|новая почта|пишет осюда|пишет отсюа|ответы тут|ответ туту|ответт тут|отвеот утт|ответ тут|ответ тту|ответ утт|переписка|пишет тут|пишут тут|jndtn nen|отвечают|отвечает|ответили|ответила|answered|пишет ут|пишут ту|пишут ут|отв тут|otv tut|primary|ответил|answear|answers|общение|answer|amswer|jответ|ответы|ответа|ответ|reply|anwer|answe|otvet|jndtn|отыет|овтет|0твет|новый|answ|отве|отв|otv|ans)(?![[:alnum:]])';
    direction_pattern text := '(?<![[:alnum:]])(?:направили сюда|написала сюда|написать сюда|писать сюда|write here|здесь|сюда|here|тут)(?![[:alnum:]])';
    restriction_pattern text := 'не\s+(пишем|писать|пиши|отвеч|работа|использ|актуал|владеет)|устарел|неактуал|недейств|больше\s+нет|неверн|карантин|quarantine|игнор|do\s+not\s+(contact|write|use)|don''t\s+(contact|write|use)|no\s+(reply|answer|working)|not\s+working|address\s+not\s+found|\yno\s+w\y';
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
    preceding_line text;
    standalone_email record;
    before_adjacent boolean;
    after_adjacent boolean;
BEGIN
    IF contact ~* restriction_pattern THEN RETURN; END IF;
    SELECT array_agg(DISTINCT lower(value)) INTO emails FROM pg_temp.primary_email_matches_v2(contact, email_pattern);
    IF cardinality(emails) = 1 AND saved_primary IS NULL THEN
        proposed_email := emails[1];
        rule := 'Single distinct repeated email';
    ELSE
        marker_count := 0;
        FOR marker IN SELECT * FROM pg_temp.primary_email_matches_v2(contact, reply_pattern) LOOP
            IF NOT EXISTS (SELECT 1 FROM pg_temp.primary_email_matches_v2(contact, email_pattern) e
                           WHERE marker.start_pos >= e.start_pos AND marker.start_pos < e.end_pos) THEN
                marker_count := marker_count + 1;
                marker_start := marker.start_pos;
                marker_end := marker.end_pos;
                marker_value := lower(marker.value);
            END IF;
        END LOOP;
        IF marker_count = 0 THEN
            FOR marker IN SELECT * FROM pg_temp.primary_email_matches_v2(contact, direction_pattern) LOOP
                IF NOT EXISTS (SELECT 1 FROM pg_temp.primary_email_matches_v2(contact, email_pattern) e
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
        -- A standalone note can bind only to the immediately preceding bare-email line.
        IF (prefix || suffix) ~ '^[ \t=:(\[)\]\-–—]*$' THEN
            preceding_line := regexp_replace(left(contact, marker_start - 1 - length(prefix)), E'(\r\n|\r|\n)$', '');
            preceding_line := regexp_replace(preceding_line, E'^.*[\r\n]', '', 's');
            IF (SELECT count(*) FROM pg_temp.primary_email_matches_v2(preceding_line, email_pattern)) = 1
               AND regexp_replace(preceding_line, email_pattern, '', 'i') ~ '^[ \t;<>]*$' THEN
                SELECT * INTO standalone_email FROM pg_temp.primary_email_matches_v2(preceding_line, email_pattern);
                prefix := preceding_line;
                suffix := '';
            END IF;
        END IF;
        SELECT * INTO before_email FROM pg_temp.primary_email_matches_v2(prefix, email_pattern) ORDER BY start_pos DESC LIMIT 1;
        SELECT * INTO after_email FROM pg_temp.primary_email_matches_v2(suffix, email_pattern) ORDER BY start_pos LIMIT 1;
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

CREATE OR REPLACE FUNCTION pg_temp.primary_contact_resolve_v1(contact text, webmaster_id uuid)
RETURNS TABLE(proposed_email text, rule text)
LANGUAGE plpgsql AS $body$
DECLARE marked record; email_match record; email_count integer;
BEGIN
    SELECT * INTO marked FROM pg_temp.primary_email_unambiguous_v2(contact, NULL);
    IF marked.proposed_email IS NOT NULL THEN
        proposed_email := marked.proposed_email;
        rule := marked.rule;
        SELECT count(*) INTO email_count FROM pg_temp.primary_email_matches_v2(contact, '[A-Z0-9._%+\-]+@[A-Z0-9.\-]+\.[A-Z]{2,}');
        IF rule = 'Single distinct repeated email' AND email_count = 1 THEN rule := 'First email in Contact'; END IF;
        RETURN NEXT; RETURN;
    END IF;
    FOR email_match IN SELECT * FROM pg_temp.primary_email_matches_v2(contact, '[A-Z0-9._%+\-]+@[A-Z0-9.\-]+\.[A-Z]{2,}') LOOP
        IF length(email_match.value) <= 320 AND lower(email_match.value) ~ '^[a-z0-9_%+\-]+(?:\.[a-z0-9_%+\-]+)*@[a-z0-9](?:[a-z0-9\-]*[a-z0-9])?(?:\.[a-z0-9](?:[a-z0-9\-]*[a-z0-9])?)*\.[a-z]{2,}$' THEN
            proposed_email := lower(email_match.value); rule := 'First email in Contact';
            RETURN NEXT; RETURN;
        END IF;
    END LOOP;
    proposed_email := regexp_replace(contact, '^[[:space:]]+|[[:space:]]+$', '', 'g');
    IF proposed_email IS NOT NULL AND proposed_email <> '' THEN
        rule := 'Contact text without email';
    ELSE
        proposed_email := 'Contact not specified (' || webmaster_id::text || ')';
        rule := 'Unique placeholder for empty Contact';
    END IF;
    RETURN NEXT;
END;
$body$;

INSERT INTO primary_email_repairs_20261014
SELECT w."Id", w."ContactRawText", w."PrimaryEmail", w.xmin::text,
       repair.proposed_email, repair.rule
FROM "Webmasters" w
CROSS JOIN LATERAL pg_temp.primary_contact_resolve_v1(w."ContactRawText", w."Id") repair
WHERE nullif(btrim(w."PrimaryEmail"), '') IS NULL;

-- One authoritative Webmaster record and a visible mirror on every affected offer.
INSERT INTO "EntityChangeHistories" ("Id", "EntityType", "EntityId", "Action", "Source", "ChangedBy", "ChangedAtUtc", "ChangesJson")
SELECT gen_random_uuid(), entity_type, entity_id, 'Updated', 'Data migration',
       'Migration:20261014102750_PopulateWebmasterPrimaryContacts', transaction_timestamp(),
       jsonb_build_array(jsonb_build_object('field', 'Primary email', 'oldValue', r.old_email, 'newValue', r.new_email),
                         jsonb_build_object('field', 'Cleanup rule', 'oldValue', NULL, 'newValue', r.rule))
FROM primary_email_repairs_20261014 r CROSS JOIN LATERAL (
    SELECT 'Webmaster'::text AS entity_type, r."Id"::text AS entity_id
    UNION ALL SELECT 'SiteWebmasterOffer', o."Id"::text FROM "SiteWebmasterOffers" o WHERE o."WebmasterId" = r."Id"
) entities;

DO $body$
DECLARE affected integer;
BEGIN
    UPDATE "Webmasters" w SET "PrimaryEmail" = r.new_email, "UpdatedAtUtc" = transaction_timestamp()
    FROM primary_email_repairs_20261014 r
    WHERE w."Id" = r."Id" AND w."ContactRawText" = r."ContactRawText"
      AND w."PrimaryEmail" IS NOT DISTINCT FROM r.old_email AND w.xmin::text = r.expected_version;
    GET DIAGNOSTICS affected = ROW_COUNT;
    IF affected <> (SELECT count(*) FROM primary_email_repairs_20261014) THEN
        RAISE EXCEPTION 'Primary email cleanup encountered changed rows; no repairs or history were committed.';
    END IF;
    RAISE NOTICE 'Primary email cleanup: % Webmaster identities updated.', affected;
END;
$body$;

DROP FUNCTION pg_temp.primary_email_unambiguous_v2(text, text);
DROP FUNCTION pg_temp.primary_contact_resolve_v1(text, uuid);
DROP FUNCTION pg_temp.primary_email_matches_v2(text, text);
