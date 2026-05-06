using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Redhead.SitesCatalog.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSiteNicheTokens : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string[]>(
                name: "NicheTokens",
                table: "Sites",
                type: "text[]",
                nullable: false,
                defaultValueSql: "ARRAY[]::text[]");

            migrationBuilder.Sql(
                """
                UPDATE "Sites"
                SET "NicheTokens" = COALESCE(
                    (
                        SELECT array_agg(deduped.token ORDER BY deduped.first_ordinal)
                        FROM (
                            SELECT normalized.token, MIN(normalized.ordinal) AS first_ordinal
                            FROM (
                                SELECT
                                    lower(regexp_replace(trim(raw.value), '\s+', ' ', 'g')) AS token,
                                    raw.ordinal
                                FROM unnest(string_to_array(COALESCE("Sites"."Niche", ''), ',')) WITH ORDINALITY AS raw(value, ordinal)
                            ) AS normalized
                            WHERE normalized.token <> ''
                              AND normalized.token NOT IN ('n/a', 'na', '-', 'none', 'null')
                            GROUP BY normalized.token
                        ) AS deduped
                    ),
                    ARRAY[]::text[]
                );
                """);

            migrationBuilder.CreateIndex(
                name: "IX_Sites_NicheTokens",
                table: "Sites",
                column: "NicheTokens")
                .Annotation("Npgsql:IndexMethod", "gin");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Sites_NicheTokens",
                table: "Sites");

            migrationBuilder.DropColumn(
                name: "NicheTokens",
                table: "Sites");
        }
    }
}
