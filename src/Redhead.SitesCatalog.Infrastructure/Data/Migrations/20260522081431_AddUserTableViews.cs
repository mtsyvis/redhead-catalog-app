using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Redhead.SitesCatalog.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddUserTableViews : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "UserTableCustomViews",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    TableKey = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Name = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    NormalizedName = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    SchemaVersion = table.Column<int>(type: "integer", nullable: false),
                    SettingsJson = table.Column<string>(type: "jsonb", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserTableCustomViews", x => x.Id);
                    table.CheckConstraint("CK_UserTableCustomViews_SchemaVersion", "\"SchemaVersion\" = 1");
                    table.CheckConstraint("CK_UserTableCustomViews_SettingsJsonLength", "length(\"SettingsJson\"::text) <= 16384");
                    table.CheckConstraint("CK_UserTableCustomViews_TableKey", "\"TableKey\" = 'sites'");
                    table.ForeignKey(
                        name: "FK_UserTableCustomViews_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserTablePreferences",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    TableKey = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ActiveViewType = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    ActiveViewKey = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserTablePreferences", x => x.Id);
                    table.CheckConstraint("CK_UserTablePreferences_ActiveViewType", "\"ActiveViewType\" IN ('system', 'custom')");
                    table.CheckConstraint("CK_UserTablePreferences_TableKey", "\"TableKey\" = 'sites'");
                    table.ForeignKey(
                        name: "FK_UserTablePreferences_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UserTableCustomViews_UserId_TableKey_NormalizedName",
                table: "UserTableCustomViews",
                columns: new[] { "UserId", "TableKey", "NormalizedName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserTablePreferences_UserId_TableKey",
                table: "UserTablePreferences",
                columns: new[] { "UserId", "TableKey" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UserTableCustomViews");

            migrationBuilder.DropTable(
                name: "UserTablePreferences");
        }
    }
}
