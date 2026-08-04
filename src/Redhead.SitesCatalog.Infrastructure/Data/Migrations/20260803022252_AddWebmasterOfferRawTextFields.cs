using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Redhead.SitesCatalog.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddWebmasterOfferRawTextFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DfLinksRawText",
                table: "SiteWebmasterOffers",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SponsoredTagRawText",
                table: "SiteWebmasterOffers",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DfLinksRawText",
                table: "SiteWebmasterOffers");

            migrationBuilder.DropColumn(
                name: "SponsoredTagRawText",
                table: "SiteWebmasterOffers");
        }
    }
}
