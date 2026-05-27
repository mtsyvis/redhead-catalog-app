using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Redhead.SitesCatalog.Infrastructure.Data.Migrations;

/// <inheritdoc />
public partial class AddCanonicalLocations : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
            migrationBuilder.AddColumn<string>(
                name: "ImportedLocationRaw",
                table: "Sites",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LocationKey",
                table: "Sites",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CanonicalLocations",
                columns: table => new
                {
                    Key = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CanonicalLocations", x => x.Key);
                });

            migrationBuilder.CreateTable(
                name: "LocationGroups",
                columns: table => new
                {
                    Key = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Kind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LocationGroups", x => x.Key);
                });

            migrationBuilder.CreateTable(
                name: "LocationGroupItems",
                columns: table => new
                {
                    GroupKey = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    LocationKey = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LocationGroupItems", x => new { x.GroupKey, x.LocationKey });
                    table.ForeignKey(
                        name: "FK_LocationGroupItems_CanonicalLocations_LocationKey",
                        column: x => x.LocationKey,
                        principalTable: "CanonicalLocations",
                        principalColumn: "Key",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_LocationGroupItems_LocationGroups_GroupKey",
                        column: x => x.GroupKey,
                        principalTable: "LocationGroups",
                        principalColumn: "Key",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Sites_LocationKey",
                table: "Sites",
                column: "LocationKey");

            migrationBuilder.CreateIndex(
                name: "IX_CanonicalLocations_DisplayName",
                table: "CanonicalLocations",
                column: "DisplayName");

            migrationBuilder.CreateIndex(
                name: "IX_CanonicalLocations_SortOrder",
                table: "CanonicalLocations",
                column: "SortOrder");

            migrationBuilder.CreateIndex(
                name: "IX_LocationGroupItems_LocationKey",
                table: "LocationGroupItems",
                column: "LocationKey");

            migrationBuilder.CreateIndex(
                name: "IX_LocationGroups_Kind_SortOrder",
                table: "LocationGroups",
                columns: new[] { "Kind", "SortOrder" });

            migrationBuilder.AddForeignKey(
                name: "FK_Sites_CanonicalLocations_LocationKey",
                table: "Sites",
                column: "LocationKey",
                principalTable: "CanonicalLocations",
                principalColumn: "Key",
                onDelete: ReferentialAction.Restrict);
        }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
            migrationBuilder.DropForeignKey(
                name: "FK_Sites_CanonicalLocations_LocationKey",
                table: "Sites");

            migrationBuilder.DropTable(
                name: "LocationGroupItems");

            migrationBuilder.DropTable(
                name: "CanonicalLocations");

            migrationBuilder.DropTable(
                name: "LocationGroups");

            migrationBuilder.DropIndex(
                name: "IX_Sites_LocationKey",
                table: "Sites");

            migrationBuilder.DropColumn(
                name: "ImportedLocationRaw",
                table: "Sites");

            migrationBuilder.DropColumn(
                name: "LocationKey",
                table: "Sites");
    }
}
