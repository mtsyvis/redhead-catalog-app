using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Redhead.SitesCatalog.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class DeactivateQuarantinedWebmasterOffers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Freeze the existing quarantine selection while preserving normal read access.
            // The offer updates, audit entries, and migration record share EF's transaction.
            migrationBuilder.Sql("""
                LOCK TABLE "Sites", "SiteWebmasterOffers", "EntityChangeHistories"
                    IN SHARE ROW EXCLUSIVE MODE;

                WITH deactivated AS (
                    UPDATE "SiteWebmasterOffers" o
                    SET "Status" = 2,
                        "UpdatedAtUtc" = transaction_timestamp(),
                        "UpdatedBy" = 'Migration:20261111051519_DeactivateQuarantinedWebmasterOffers'
                    FROM "Sites" s
                    WHERE o."SiteDomain" = s."Domain"
                        AND s."IsQuarantined" = TRUE
                        AND o."Status" = 1
                    RETURNING o."Id"
                )
                INSERT INTO "EntityChangeHistories"
                    ("Id", "EntityType", "EntityId", "Action", "Source", "ChangedBy", "ChangedAtUtc", "ChangesJson")
                SELECT gen_random_uuid(), 'SiteWebmasterOffer', "Id"::text, 'Updated', 'Data migration',
                    'Migration:20261111051519_DeactivateQuarantinedWebmasterOffers', transaction_timestamp(),
                    jsonb_build_array(
                        jsonb_build_object('field', 'Status', 'oldValue', 'Active', 'newValue', 'Inactive'),
                        jsonb_build_object('field', 'Cleanup rule', 'oldValue', NULL,
                            'newValue', 'Technical correction: deactivate existing Active webmaster offers because their catalog site is quarantined.'))
                FROM deactivated;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Do not reactivate offers that may have been reviewed or edited since the repair.
            migrationBuilder.Sql("""
                DO $body$
                BEGIN
                    IF EXISTS (SELECT 1 FROM "EntityChangeHistories"
                        WHERE "ChangedBy" = 'Migration:20261111051519_DeactivateQuarantinedWebmasterOffers') THEN
                        RAISE EXCEPTION 'Quarantined offer repair cannot be rolled back automatically. Restore the pre-migration backup or apply a reviewed correction.';
                    END IF;
                END;
                $body$;
                """);
        }
    }
}
