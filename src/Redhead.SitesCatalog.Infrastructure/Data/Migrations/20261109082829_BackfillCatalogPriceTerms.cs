using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Redhead.SitesCatalog.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class BackfillCatalogPriceTerms : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            const string resource = "Redhead.SitesCatalog.Infrastructure.Data.Migrations.Sql.BackfillCatalogPriceTerms.Up.sql";
            using var stream = typeof(BackfillCatalogPriceTerms).Assembly.GetManifestResourceStream(resource)
                ?? throw new InvalidOperationException($"Missing embedded migration resource: {resource}");
            using var reader = new StreamReader(stream);
            migrationBuilder.Sql(reader.ReadToEnd());
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // An empty repair can be removed; changed data must not silently lose its migration record.
            migrationBuilder.Sql("""
                DO $body$
                BEGIN
                    IF EXISTS (SELECT 1 FROM "EntityChangeHistories"
                        WHERE "ChangedBy" = 'Migration:20261109082829_BackfillCatalogPriceTerms') THEN
                        RAISE EXCEPTION 'Catalog price term repair cannot be rolled back automatically. Restore the pre-migration backup or apply a reviewed correction.';
                    END IF;
                END;
                $body$;
                """);
        }
    }
}
