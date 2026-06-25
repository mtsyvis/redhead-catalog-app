using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Redhead.SitesCatalog.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddLiteUserRoleAndUsage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "LiteMultiSearchUsages",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    MonthStartUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DomainsUsed = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LiteMultiSearchUsages", x => new { x.UserId, x.MonthStartUtc });
                    table.CheckConstraint("CK_LiteMultiSearchUsages_DomainsUsed_NonNegative", "\"DomainsUsed\" >= 0");
                    table.ForeignKey(
                        name: "FK_LiteMultiSearchUsages_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "RoleSettings",
                columns: new[] { "RoleName", "DailyExportOperationsLimit", "DailyUniqueExportedDomainsLimit", "ExportLimitMode", "ExportLimitRows", "WeeklyExportOperationsLimit", "WeeklyUniqueExportedDomainsLimit" },
                values: new object[] { "Lite", null, null, 1, null, null, null });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LiteMultiSearchUsages");

            migrationBuilder.DeleteData(
                table: "RoleSettings",
                keyColumn: "RoleName",
                keyValue: "Lite");
        }
    }
}
