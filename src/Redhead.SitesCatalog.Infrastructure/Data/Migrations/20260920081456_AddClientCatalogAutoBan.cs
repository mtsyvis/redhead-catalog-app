using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Redhead.SitesCatalog.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddClientCatalogAutoBan : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ClientCatalogAutoBanResetAtUtc",
                table: "AspNetUsers",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DisabledAtUtc",
                table: "AspNetUsers",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DisabledReason",
                table: "AspNetUsers",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ClientCatalogAutoBans",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<string>(type: "text", nullable: false),
                    DetectedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UniqueSites = table.Column<int>(type: "integer", nullable: false),
                    Threshold = table.Column<int>(type: "integer", nullable: false),
                    ReviewedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ReviewedByUserId = table.Column<string>(type: "text", nullable: true),
                    EmailSentAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    NextEmailAttemptAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClientCatalogAutoBans", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ClientCatalogAutoBans_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ClientCatalogProtectionSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AutoBanEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    AutoBanUniqueSitesPer24Hours = table.Column<int>(type: "integer", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedByUserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClientCatalogProtectionSettings", x => x.Id);
                    table.CheckConstraint("CK_ClientCatalogProtectionSettings_AutoBanThreshold_Positive", "\"AutoBanUniqueSitesPer24Hours\" > 0");
                    table.CheckConstraint("CK_ClientCatalogProtectionSettings_Singleton", "\"Id\" = 1");
                });

            migrationBuilder.InsertData(
                table: "ClientCatalogProtectionSettings",
                columns: new[] { "Id", "AutoBanEnabled", "AutoBanUniqueSitesPer24Hours" },
                values: new object[] { 1, false, 20000 });

            migrationBuilder.CreateIndex(
                name: "IX_ClientCatalogAutoBans_UserId",
                table: "ClientCatalogAutoBans",
                column: "UserId",
                unique: true,
                filter: "\"ReviewedAtUtc\" IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ClientCatalogAutoBans");

            migrationBuilder.DropTable(
                name: "ClientCatalogProtectionSettings");

            migrationBuilder.DropColumn(
                name: "ClientCatalogAutoBanResetAtUtc",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "DisabledAtUtc",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "DisabledReason",
                table: "AspNetUsers");
        }
    }
}
