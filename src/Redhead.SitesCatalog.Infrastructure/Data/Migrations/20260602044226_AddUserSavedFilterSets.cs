using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Redhead.SitesCatalog.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddUserSavedFilterSets : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "UserSavedFilterSets",
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
                    table.PrimaryKey("PK_UserSavedFilterSets", x => x.Id);
                    table.CheckConstraint("CK_UserSavedFilterSets_SchemaVersion", "\"SchemaVersion\" = 1");
                    table.CheckConstraint("CK_UserSavedFilterSets_SettingsJsonLength", "length(\"SettingsJson\"::text) <= 2097152");
                    table.CheckConstraint("CK_UserSavedFilterSets_TableKey", "\"TableKey\" = 'sites'");
                    table.ForeignKey(
                        name: "FK_UserSavedFilterSets_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UserSavedFilterSets_UserId_TableKey_NormalizedName",
                table: "UserSavedFilterSets",
                columns: new[] { "UserId", "TableKey", "NormalizedName" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UserSavedFilterSets");
        }
    }
}
