-- Frozen one-time repair. All selection, price changes, and Site history share EF's transaction.
-- Serialize catalog/offer writes while continuing to allow reads.
LOCK TABLE "Sites", "SitePriceOptions", "SiteServiceAvailabilities",
    "SiteWebmasterOffers", "WebmasterOfferPrices", "EntityChangeHistories"
    IN SHARE ROW EXCLUSIVE MODE;

CREATE TEMP TABLE catalog_price_term_repair ON COMMIT DROP AS
WITH active_terms AS (
    SELECT o."SiteDomain" AS domain, o."TermType", o."TermValue", o."TermUnit",
        CASE
            WHEN o."TermType" IS NULL AND o."TermValue" IS NULL AND o."TermUnit" IS NULL THEN 'unknown'
            WHEN o."TermType" = 1 AND o."TermValue" IS NULL AND o."TermUnit" IS NULL THEN 'permanent'
            WHEN o."TermType" = 2 AND o."TermValue" > 0 AND o."TermUnit" = 1
                THEN 'finite:' || o."TermValue"::text || ':year'
            ELSE 'invalid'
        END AS term_key
    FROM "SiteWebmasterOffers" o
    WHERE o."Status" = 1
), targets AS (
    -- Unknown, malformed, and conflicting Active terms disqualify the entire site.
    SELECT domain, min(term_key) AS term_key, min("TermType") AS term_type,
        min("TermValue") AS term_value, min("TermUnit") AS term_unit
    FROM active_terms
    GROUP BY domain
    HAVING count(DISTINCT term_key) = 1 AND min(term_key) NOT IN ('unknown', 'invalid')
), catalog AS (
    SELECT t.domain, count(DISTINCT p."TermKey") AS term_count
    FROM targets t
    JOIN "SitePriceOptions" p ON p."SiteDomain" = t.domain
    LEFT JOIN "SiteServiceAvailabilities" a
        ON a."SiteDomain" = p."SiteDomain" AND a."ServiceType" = p."PriceType"
    GROUP BY t.domain
    HAVING bool_and(p."TermKey" IN ('unknown', t.term_key))
        AND bool_and(a."Status" IS NULL OR a."Status" = 1)
), unknown_prices AS (
    SELECT p."Id" AS price_id, p."SiteDomain" AS domain, p."PriceType" AS price_type,
        p."AmountUsd" AS amount, t.term_key, t.term_type, t.term_value, t.term_unit,
        c.term_count, existing."Id" AS target_id, existing."AmountUsd" AS target_amount,
        EXISTS (
            SELECT 1
            FROM "SiteWebmasterOffers" o
            JOIN "WebmasterOfferPrices" op ON op."SiteWebmasterOfferId" = o."Id"
            WHERE o."SiteDomain" = p."SiteDomain" AND o."Status" = 1
                AND op."AvailabilityStatus" = 1 AND op."WebmasterPriceUsd" > 0
                -- Catalog/offer enums have different values for insertion and dating.
                AND op."PriceType" = CASE p."PriceType"
                    WHEN 0 THEN 0 WHEN 1 THEN 1 WHEN 2 THEN 2
                    WHEN 3 THEN 4 WHEN 4 THEN 5 WHEN 5 THEN 3 END
                AND op."TermType" = t.term_type
                AND op."TermValue" IS NOT DISTINCT FROM t.term_value
                AND op."TermUnit" IS NOT DISTINCT FROM t.term_unit
        ) AS has_numeric_offer
    FROM "SitePriceOptions" p
    JOIN targets t ON t.domain = p."SiteDomain"
    JOIN catalog c ON c.domain = p."SiteDomain"
    LEFT JOIN "SitePriceOptions" existing ON existing."SiteDomain" = p."SiteDomain"
        AND existing."PriceType" = p."PriceType" AND existing."TermKey" = t.term_key
    WHERE p."TermKey" = 'unknown'
), eligible_sites AS (
    SELECT domain
    FROM unknown_prices
    GROUP BY domain
    -- Equal-amount duplicates can be merged; any differing amount skips the whole site.
    HAVING bool_and(target_id IS NULL OR target_amount = amount)
        -- Do not create multiple terms on a site that previously had only No term.
        AND (max(term_count) > 1 OR bool_and(has_numeric_offer))
)
SELECT p.*
FROM unknown_prices p JOIN eligible_sites s ON s.domain = p.domain
WHERE p.has_numeric_offer;

-- Use the same field keys and amount formatting as CaptureSite. A moved price appears as
-- removal from No term and addition at the known term; a merged duplicate only as removal.
WITH changes AS (
    SELECT domain, 'Price · ' || CASE price_type
            WHEN 0 THEN 'Main' WHEN 1 THEN 'Casino' WHEN 2 THEN 'Crypto'
            WHEN 3 THEN 'LinkInsertion' WHEN 4 THEN 'LinkInsertionCasino' WHEN 5 THEN 'Dating'
        END || ' · unknown' AS field,
        rtrim(rtrim(amount::text, '0'), '.') AS old_value, NULL::text AS new_value
    FROM catalog_price_term_repair
    UNION ALL
    SELECT domain, 'Price · ' || CASE price_type
            WHEN 0 THEN 'Main' WHEN 1 THEN 'Casino' WHEN 2 THEN 'Crypto'
            WHEN 3 THEN 'LinkInsertion' WHEN 4 THEN 'LinkInsertionCasino' WHEN 5 THEN 'Dating'
        END || ' · ' || term_key,
        NULL, rtrim(rtrim(amount::text, '0'), '.')
    FROM catalog_price_term_repair WHERE target_id IS NULL
)
INSERT INTO "EntityChangeHistories"
    ("Id", "EntityType", "EntityId", "Action", "Source", "ChangedBy", "ChangedAtUtc", "ChangesJson")
SELECT gen_random_uuid(), 'Site', domain, 'Updated', 'Data migration',
    'Migration:20261109082829_BackfillCatalogPriceTerms', transaction_timestamp(),
    jsonb_agg(jsonb_build_object('field', field, 'oldValue', old_value, 'newValue', new_value) ORDER BY field)
        || jsonb_build_array(jsonb_build_object('field', 'Cleanup rule', 'oldValue', NULL,
            'newValue', 'Technical correction: restore missing catalog price terms from unanimous Active webmaster offers for the same service. Selling amounts are unchanged; identical target-term duplicates are merged.'))
FROM changes GROUP BY domain;

DELETE FROM "SitePriceOptions" p
USING catalog_price_term_repair r
WHERE p."Id" = r.price_id AND r.target_id IS NOT NULL;

UPDATE "SitePriceOptions" p
SET "TermKey" = r.term_key, "TermType" = r.term_type, "TermValue" = r.term_value,
    "TermUnit" = r.term_unit, "UpdatedAtUtc" = transaction_timestamp()
FROM catalog_price_term_repair r
WHERE p."Id" = r.price_id AND r.target_id IS NULL;

UPDATE "Sites" s
SET "UpdatedAtUtc" = transaction_timestamp(),
    "UpdatedBy" = 'Migration:20261109082829_BackfillCatalogPriceTerms'
WHERE s."Domain" IN (SELECT domain FROM catalog_price_term_repair);

DO $body$
BEGIN
    RAISE NOTICE 'Catalog term repair: % sites, % moved prices, % identical duplicates merged',
        (SELECT count(DISTINCT domain) FROM catalog_price_term_repair),
        (SELECT count(*) FROM catalog_price_term_repair WHERE target_id IS NULL),
        (SELECT count(*) FROM catalog_price_term_repair WHERE target_id IS NOT NULL);
END;
$body$;
