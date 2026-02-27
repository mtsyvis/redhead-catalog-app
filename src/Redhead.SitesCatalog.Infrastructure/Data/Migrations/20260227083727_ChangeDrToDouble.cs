using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Redhead.SitesCatalog.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class ChangeDrToDouble : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<double>(
                name: "DR",
                table: "Sites",
                type: "double precision",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "DR",
                table: "Sites",
                type: "integer",
                nullable: false,
                oldClrType: typeof(double),
                oldType: "double precision");
        }
    }
}
