using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Redhead.SitesCatalog.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class BackfillJuneFourthSiteMetrics : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "SnapshotMonth",
                table: "SiteMetricSnapshots",
                newName: "SnapshotDate");

            migrationBuilder.RenameIndex(
                name: "IX_SiteMetricSnapshots_Domain_SnapshotMonth",
                table: "SiteMetricSnapshots",
                newName: "IX_SiteMetricSnapshots_Domain_SnapshotDate");

            migrationBuilder.Sql(
                """
                INSERT INTO "SiteMetricSnapshots" (
                    "Id",
                    "Domain",
                    "SnapshotDate",
                    "Traffic",
                    "DomainRating",
                    "Source",
                    "AhrefsSyncRunId",
                    "FetchedAt")
                SELECT
                    gen_random_uuid(),
                    site."Domain",
                    DATE '2026-06-04',
                    site."Traffic",
                    site."DR",
                    'AhrefsBaselineImport',
                    NULL,
                    TIMESTAMPTZ '2026-06-04 00:00:00+00'
                FROM "Sites" AS site
                WHERE TRUE
                ON CONFLICT ("Domain", "SnapshotDate") DO NOTHING;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DELETE FROM "SiteMetricSnapshots"
                WHERE "SnapshotDate" = DATE '2026-06-04'
                  AND "Source" = 'AhrefsBaselineImport'
                  AND "AhrefsSyncRunId" IS NULL;
                """);

            migrationBuilder.RenameColumn(
                name: "SnapshotDate",
                table: "SiteMetricSnapshots",
                newName: "SnapshotMonth");

            migrationBuilder.RenameIndex(
                name: "IX_SiteMetricSnapshots_Domain_SnapshotDate",
                table: "SiteMetricSnapshots",
                newName: "IX_SiteMetricSnapshots_Domain_SnapshotMonth");
        }
    }
}
