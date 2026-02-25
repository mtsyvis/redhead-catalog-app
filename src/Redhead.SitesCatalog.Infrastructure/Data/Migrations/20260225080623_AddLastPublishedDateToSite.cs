using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Redhead.SitesCatalog.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddLastPublishedDateToSite : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "LastPublishedDate",
                table: "Sites",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "LastPublishedDateIsMonthOnly",
                table: "Sites",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LastPublishedDate",
                table: "Sites");

            migrationBuilder.DropColumn(
                name: "LastPublishedDateIsMonthOnly",
                table: "Sites");
        }
    }
}
