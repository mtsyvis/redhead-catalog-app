using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Redhead.SitesCatalog.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTermAwarePricing : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SitePriceOptions",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SiteDomain = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    PriceType = table.Column<short>(type: "smallint", nullable: false),
                    TermKey = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    TermType = table.Column<short>(type: "smallint", nullable: true),
                    TermValue = table.Column<int>(type: "integer", nullable: true),
                    TermUnit = table.Column<short>(type: "smallint", nullable: true),
                    AmountUsd = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SitePriceOptions", x => x.Id);
                    table.CheckConstraint("CK_SitePriceOptions_AmountUsd_Positive", "\"AmountUsd\" > 0");
                    table.CheckConstraint("CK_SitePriceOptions_Term_Consistency", "(\"TermKey\" = 'unknown' AND \"TermType\" IS NULL AND \"TermValue\" IS NULL AND \"TermUnit\" IS NULL) OR (\"TermKey\" = 'permanent' AND \"TermType\" = 1 AND \"TermValue\" IS NULL AND \"TermUnit\" IS NULL) OR (\"TermType\" = 2 AND \"TermValue\" IS NOT NULL AND \"TermValue\" > 0 AND \"TermUnit\" = 1 AND \"TermKey\" = ('finite:' || \"TermValue\"::text || ':year'))");
                    table.ForeignKey(
                        name: "FK_SitePriceOptions_Sites_SiteDomain",
                        column: x => x.SiteDomain,
                        principalTable: "Sites",
                        principalColumn: "Domain",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SiteServiceAvailabilities",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SiteDomain = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    ServiceType = table.Column<short>(type: "smallint", nullable: false),
                    Status = table.Column<short>(type: "smallint", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SiteServiceAvailabilities", x => x.Id);
                    table.CheckConstraint("CK_SiteServiceAvailabilities_ServiceType_NotMain", "\"ServiceType\" <> 0");
                    table.ForeignKey(
                        name: "FK_SiteServiceAvailabilities_Sites_SiteDomain",
                        column: x => x.SiteDomain,
                        principalTable: "Sites",
                        principalColumn: "Domain",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SitePriceOptions_PriceType_TermKey_AmountUsd",
                table: "SitePriceOptions",
                columns: new[] { "PriceType", "TermKey", "AmountUsd" });

            migrationBuilder.CreateIndex(
                name: "IX_SitePriceOptions_SiteDomain_PriceType",
                table: "SitePriceOptions",
                columns: new[] { "SiteDomain", "PriceType" });

            migrationBuilder.CreateIndex(
                name: "IX_SitePriceOptions_SiteDomain_PriceType_TermKey",
                table: "SitePriceOptions",
                columns: new[] { "SiteDomain", "PriceType", "TermKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SiteServiceAvailabilities_SiteDomain_ServiceType",
                table: "SiteServiceAvailabilities",
                columns: new[] { "SiteDomain", "ServiceType" },
                unique: true);

            migrationBuilder.Sql("""
                INSERT INTO "SitePriceOptions" (
                    "SiteDomain",
                    "PriceType",
                    "TermKey",
                    "TermType",
                    "TermValue",
                    "TermUnit",
                    "AmountUsd",
                    "CreatedAtUtc",
                    "UpdatedAtUtc")
                SELECT
                    s."Domain",
                    price."PriceType",
                    price."TermKey",
                    price."TermType",
                    price."TermValue",
                    price."TermUnit",
                    price."AmountUsd",
                    s."CreatedAtUtc",
                    s."UpdatedAtUtc"
                FROM "Sites" AS s
                CROSS JOIN LATERAL (
                    SELECT
                        CASE
                            WHEN s."TermType" = 1 THEN 'permanent'
                            WHEN s."TermType" = 2 AND s."TermValue" IS NOT NULL AND s."TermValue" > 0 AND s."TermUnit" = 1 THEN 'finite:' || s."TermValue"::text || ':year'
                            ELSE 'unknown'
                        END AS "TermKey",
                        CASE
                            WHEN s."TermType" = 1 THEN 1::smallint
                            WHEN s."TermType" = 2 AND s."TermValue" IS NOT NULL AND s."TermValue" > 0 AND s."TermUnit" = 1 THEN 2::smallint
                            ELSE NULL::smallint
                        END AS "TermType",
                        CASE
                            WHEN s."TermType" = 2 AND s."TermValue" IS NOT NULL AND s."TermValue" > 0 AND s."TermUnit" = 1 THEN s."TermValue"
                            ELSE NULL::integer
                        END AS "TermValue",
                        CASE
                            WHEN s."TermType" = 2 AND s."TermValue" IS NOT NULL AND s."TermValue" > 0 AND s."TermUnit" = 1 THEN 1::smallint
                            ELSE NULL::smallint
                        END AS "TermUnit"
                ) AS current_term
                CROSS JOIN LATERAL (
                    VALUES
                        (0::smallint, current_term."TermKey", current_term."TermType", current_term."TermValue", current_term."TermUnit", s."PriceUsd"),
                        (1::smallint, current_term."TermKey", current_term."TermType", current_term."TermValue", current_term."TermUnit", s."PriceCasino"),
                        (2::smallint, current_term."TermKey", current_term."TermType", current_term."TermValue", current_term."TermUnit", s."PriceCrypto"),
                        (5::smallint, current_term."TermKey", current_term."TermType", current_term."TermValue", current_term."TermUnit", s."PriceDating"),
                        (3::smallint, 'unknown', NULL::smallint, NULL::integer, NULL::smallint, s."PriceLinkInsert"),
                        (4::smallint, 'unknown', NULL::smallint, NULL::integer, NULL::smallint, s."PriceLinkInsertCasino")
                ) AS price("PriceType", "TermKey", "TermType", "TermValue", "TermUnit", "AmountUsd")
                WHERE price."AmountUsd" IS NOT NULL AND price."AmountUsd" > 0
                ON CONFLICT ("SiteDomain", "PriceType", "TermKey") DO NOTHING;
                """);

            migrationBuilder.Sql("""
                INSERT INTO "SiteServiceAvailabilities" (
                    "SiteDomain",
                    "ServiceType",
                    "Status",
                    "CreatedAtUtc",
                    "UpdatedAtUtc")
                SELECT
                    s."Domain",
                    availability."ServiceType",
                    availability."Status",
                    s."CreatedAtUtc",
                    s."UpdatedAtUtc"
                FROM "Sites" AS s
                CROSS JOIN LATERAL (
                    VALUES
                        (1::smallint, CASE WHEN s."PriceCasino" IS NOT NULL AND s."PriceCasino" > 0 THEN 1::smallint ELSE s."PriceCasinoStatus" END),
                        (2::smallint, CASE WHEN s."PriceCrypto" IS NOT NULL AND s."PriceCrypto" > 0 THEN 1::smallint ELSE s."PriceCryptoStatus" END),
                        (3::smallint, CASE WHEN s."PriceLinkInsert" IS NOT NULL AND s."PriceLinkInsert" > 0 THEN 1::smallint ELSE s."PriceLinkInsertStatus" END),
                        (4::smallint, CASE WHEN s."PriceLinkInsertCasino" IS NOT NULL AND s."PriceLinkInsertCasino" > 0 THEN 1::smallint ELSE s."PriceLinkInsertCasinoStatus" END),
                        (5::smallint, CASE WHEN s."PriceDating" IS NOT NULL AND s."PriceDating" > 0 THEN 1::smallint ELSE s."PriceDatingStatus" END)
                ) AS availability("ServiceType", "Status")
                ON CONFLICT ("SiteDomain", "ServiceType") DO NOTHING;
                """);

            migrationBuilder.Sql("""
                DO $$
                DECLARE
                    sites_count bigint;
                    expected_availability_rows bigint;
                    actual_availability_rows bigint;
                    missing_availability_rows bigint;

                    expected_price_rows bigint;
                    actual_price_rows bigint;
                    missing_price_rows bigint;
                BEGIN
                    SELECT COUNT(*)
                    INTO sites_count
                    FROM "Sites";

                    expected_availability_rows := sites_count * 5;

                    SELECT COUNT(*)
                    INTO actual_availability_rows
                    FROM "SiteServiceAvailabilities";

                    WITH expected AS (
                        SELECT
                            s."Domain" AS "SiteDomain",
                            service."ServiceType"
                        FROM "Sites" s
                        CROSS JOIN (
                            VALUES
                                (1::smallint),
                                (2::smallint),
                                (3::smallint),
                                (4::smallint),
                                (5::smallint)
                        ) AS service("ServiceType")
                    )
                    SELECT COUNT(*)
                    INTO missing_availability_rows
                    FROM expected e
                    LEFT JOIN "SiteServiceAvailabilities" a
                        ON a."SiteDomain" = e."SiteDomain"
                       AND a."ServiceType" = e."ServiceType"
                    WHERE a."Id" IS NULL;

                    IF actual_availability_rows <> expected_availability_rows
                       OR missing_availability_rows <> 0 THEN
                        RAISE EXCEPTION
                            'SiteServiceAvailabilities migration validation failed. Sites: %, expected rows: %, actual rows: %, missing rows: %',
                            sites_count,
                            expected_availability_rows,
                            actual_availability_rows,
                            missing_availability_rows;
                    END IF;

                    WITH expected AS (
                        SELECT
                            s."Domain" AS "SiteDomain",
                            price."PriceType",
                            price."TermKey",
                            price."AmountUsd"
                        FROM "Sites" AS s
                        CROSS JOIN LATERAL (
                            SELECT
                                CASE
                                    WHEN s."TermType" = 1 THEN 'permanent'
                                    WHEN s."TermType" = 2
                                        AND s."TermValue" IS NOT NULL
                                        AND s."TermValue" > 0
                                        AND s."TermUnit" = 1
                                        THEN 'finite:' || s."TermValue"::text || ':year'
                                    ELSE 'unknown'
                                END AS "TermKey"
                        ) AS current_term
                        CROSS JOIN LATERAL (
                            VALUES
                                (0::smallint, current_term."TermKey", s."PriceUsd"),
                                (1::smallint, current_term."TermKey", s."PriceCasino"),
                                (2::smallint, current_term."TermKey", s."PriceCrypto"),
                                (5::smallint, current_term."TermKey", s."PriceDating"),
                                (3::smallint, 'unknown', s."PriceLinkInsert"),
                                (4::smallint, 'unknown', s."PriceLinkInsertCasino")
                        ) AS price("PriceType", "TermKey", "AmountUsd")
                        WHERE price."AmountUsd" IS NOT NULL
                          AND price."AmountUsd" > 0
                    )
                    SELECT COUNT(*)
                    INTO expected_price_rows
                    FROM expected;

                    SELECT COUNT(*)
                    INTO actual_price_rows
                    FROM "SitePriceOptions";

                    WITH expected AS (
                        SELECT
                            s."Domain" AS "SiteDomain",
                            price."PriceType",
                            price."TermKey",
                            price."AmountUsd"
                        FROM "Sites" AS s
                        CROSS JOIN LATERAL (
                            SELECT
                                CASE
                                    WHEN s."TermType" = 1 THEN 'permanent'
                                    WHEN s."TermType" = 2
                                        AND s."TermValue" IS NOT NULL
                                        AND s."TermValue" > 0
                                        AND s."TermUnit" = 1
                                        THEN 'finite:' || s."TermValue"::text || ':year'
                                    ELSE 'unknown'
                                END AS "TermKey"
                        ) AS current_term
                        CROSS JOIN LATERAL (
                            VALUES
                                (0::smallint, current_term."TermKey", s."PriceUsd"),
                                (1::smallint, current_term."TermKey", s."PriceCasino"),
                                (2::smallint, current_term."TermKey", s."PriceCrypto"),
                                (5::smallint, current_term."TermKey", s."PriceDating"),
                                (3::smallint, 'unknown', s."PriceLinkInsert"),
                                (4::smallint, 'unknown', s."PriceLinkInsertCasino")
                        ) AS price("PriceType", "TermKey", "AmountUsd")
                        WHERE price."AmountUsd" IS NOT NULL
                          AND price."AmountUsd" > 0
                    )
                    SELECT COUNT(*)
                    INTO missing_price_rows
                    FROM expected e
                    LEFT JOIN "SitePriceOptions" p
                        ON p."SiteDomain" = e."SiteDomain"
                       AND p."PriceType" = e."PriceType"
                       AND p."TermKey" = e."TermKey"
                    WHERE p."Id" IS NULL;

                    IF actual_price_rows <> expected_price_rows
                       OR missing_price_rows <> 0 THEN
                        RAISE EXCEPTION
                            'SitePriceOptions migration validation failed. Expected rows: %, actual rows: %, missing rows: %',
                            expected_price_rows,
                            actual_price_rows,
                            missing_price_rows;
                    END IF;

                    RAISE NOTICE
                        'Term-aware pricing migration validation passed. Sites: %, availability rows: %, price option rows: %',
                        sites_count,
                        actual_availability_rows,
                        actual_price_rows;
                END $$;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SitePriceOptions");

            migrationBuilder.DropTable(
                name: "SiteServiceAvailabilities");
        }
    }
}
