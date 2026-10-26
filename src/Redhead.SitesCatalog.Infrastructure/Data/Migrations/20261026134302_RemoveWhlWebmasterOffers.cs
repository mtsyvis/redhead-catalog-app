using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Redhead.SitesCatalog.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class RemoveWhlWebmasterOffers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Prices and mailbox links are deleted by their existing ON DELETE CASCADE FKs.
            // Keep technical Webmasters so re-imports retain their primary contact.
            migrationBuilder.Sql(
                """
                DELETE FROM "SiteWebmasterOffers" AS o
                USING "Webmasters" AS w
                WHERE o."WebmasterId" = w."Id"
                  AND w."PrimaryEmail" = 'WHL - по вопросом публикации к Лене';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            throw new NotSupportedException(
                "Deleted WHL webmaster offers cannot be restored by a migration rollback. " +
                "Re-import the corrected source CSV or restore a database backup.");
        }
    }
}
