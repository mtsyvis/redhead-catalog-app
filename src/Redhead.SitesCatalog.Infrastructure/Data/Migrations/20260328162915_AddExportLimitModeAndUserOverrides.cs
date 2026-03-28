using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Redhead.SitesCatalog.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddExportLimitModeAndUserOverrides : Migration
    {
        // ExportLimitMode enum values (must stay in sync with Domain/Enums/ExportLimitMode.cs)
        private const int ModeDisabled = 1;
        private const int ModeLimited = 2;
        private const int ModeUnlimited = 3;

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Step 1: make ExportLimitRows nullable (preserves existing values for backfill below)
            migrationBuilder.AlterColumn<int>(
                name: "ExportLimitRows",
                table: "RoleSettings",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            // Step 2: add ExportLimitMode with temporary default 0
            // (invalid enum value; will be overwritten by backfill in step 3)
            migrationBuilder.AddColumn<int>(
                name: "ExportLimitMode",
                table: "RoleSettings",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            // Step 3: backfill ExportLimitMode from legacy ExportLimitRows numeric semantics
            //   old 0          => Disabled (1)
            //   old >= 1000000 => Unlimited (3)
            //   old 1..999999  => Limited (2), rows preserved as-is
            migrationBuilder.Sql($@"
UPDATE ""RoleSettings""
SET ""ExportLimitMode"" = CASE
    WHEN ""ExportLimitRows"" IS NULL   THEN {ModeUnlimited}
    WHEN ""ExportLimitRows"" = 0       THEN {ModeDisabled}
    WHEN ""ExportLimitRows"" >= 1000000 THEN {ModeUnlimited}
    ELSE {ModeLimited}
END;
");

            // Step 4: null out rows for Disabled and Unlimited (rows is only meaningful for Limited)
            migrationBuilder.Sql($@"
UPDATE ""RoleSettings""
SET ""ExportLimitRows"" = NULL
WHERE ""ExportLimitMode"" <> {ModeLimited};
");

            // Step 5: remove the temporary default (column is already fully populated)
            migrationBuilder.AlterColumn<int>(
                name: "ExportLimitMode",
                table: "RoleSettings",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldDefaultValue: 0);

            // Step 6: check constraints for RoleSettings
            migrationBuilder.AddCheckConstraint(
                name: "CK_RoleSettings_ExportLimitMode_ValidValue",
                table: "RoleSettings",
                sql: $@"""ExportLimitMode"" IN ({ModeDisabled}, {ModeLimited}, {ModeUnlimited})");

            migrationBuilder.AddCheckConstraint(
                name: "CK_RoleSettings_ExportLimitRows_LimitedRequiresRows",
                table: "RoleSettings",
                sql: $@"""ExportLimitMode"" <> {ModeLimited} OR (""ExportLimitRows"" IS NOT NULL AND ""ExportLimitRows"" > 0)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_RoleSettings_ExportLimitRows_NonLimitedRequiresNullRows",
                table: "RoleSettings",
                sql: $@"""ExportLimitMode"" = {ModeLimited} OR ""ExportLimitRows"" IS NULL");

            // Step 7: add user override columns (nullable; all existing users inherit from role)
            migrationBuilder.AddColumn<int>(
                name: "ExportLimitOverrideMode",
                table: "AspNetUsers",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ExportLimitRowsOverride",
                table: "AspNetUsers",
                type: "integer",
                nullable: true);

            // Step 8: check constraints for user overrides
            migrationBuilder.AddCheckConstraint(
                name: "CK_AspNetUsers_ExportLimitOverride_ValidMode",
                table: "AspNetUsers",
                sql: $@"""ExportLimitOverrideMode"" IS NULL OR ""ExportLimitOverrideMode"" IN ({ModeDisabled}, {ModeLimited}, {ModeUnlimited})");

            migrationBuilder.AddCheckConstraint(
                name: "CK_AspNetUsers_ExportLimitOverride_LimitedRequiresRows",
                table: "AspNetUsers",
                sql: $@"""ExportLimitOverrideMode"" IS NULL OR ""ExportLimitOverrideMode"" <> {ModeLimited} OR (""ExportLimitRowsOverride"" IS NOT NULL AND ""ExportLimitRowsOverride"" > 0)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_AspNetUsers_ExportLimitOverride_NonLimitedRequiresNullRows",
                table: "AspNetUsers",
                sql: $@"""ExportLimitOverrideMode"" IS NULL OR ""ExportLimitOverrideMode"" = {ModeLimited} OR ""ExportLimitRowsOverride"" IS NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_AspNetUsers_ExportLimitOverride_NullModeRequiresNullRows",
                table: "AspNetUsers",
                sql: @"""ExportLimitOverrideMode"" IS NOT NULL OR ""ExportLimitRowsOverride"" IS NULL");

            // Step 9: update seed-data rows to their correct explicit mode values
            migrationBuilder.UpdateData(
                table: "RoleSettings",
                keyColumn: "RoleName",
                keyValue: "Admin",
                columns: new[] { "ExportLimitMode", "ExportLimitRows" },
                values: new object[] { ModeUnlimited, null });

            migrationBuilder.UpdateData(
                table: "RoleSettings",
                keyColumn: "RoleName",
                keyValue: "Client",
                column: "ExportLimitMode",
                value: ModeLimited);

            migrationBuilder.UpdateData(
                table: "RoleSettings",
                keyColumn: "RoleName",
                keyValue: "Internal",
                column: "ExportLimitMode",
                value: ModeLimited);

            migrationBuilder.UpdateData(
                table: "RoleSettings",
                keyColumn: "RoleName",
                keyValue: "SuperAdmin",
                columns: new[] { "ExportLimitMode", "ExportLimitRows" },
                values: new object[] { ModeUnlimited, null });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_AspNetUsers_ExportLimitOverride_NullModeRequiresNullRows",
                table: "AspNetUsers");

            migrationBuilder.DropCheckConstraint(
                name: "CK_AspNetUsers_ExportLimitOverride_NonLimitedRequiresNullRows",
                table: "AspNetUsers");

            migrationBuilder.DropCheckConstraint(
                name: "CK_AspNetUsers_ExportLimitOverride_LimitedRequiresRows",
                table: "AspNetUsers");

            migrationBuilder.DropCheckConstraint(
                name: "CK_AspNetUsers_ExportLimitOverride_ValidMode",
                table: "AspNetUsers");

            migrationBuilder.DropCheckConstraint(
                name: "CK_RoleSettings_ExportLimitRows_NonLimitedRequiresNullRows",
                table: "RoleSettings");

            migrationBuilder.DropCheckConstraint(
                name: "CK_RoleSettings_ExportLimitRows_LimitedRequiresRows",
                table: "RoleSettings");

            migrationBuilder.DropCheckConstraint(
                name: "CK_RoleSettings_ExportLimitMode_ValidValue",
                table: "RoleSettings");

            migrationBuilder.DropColumn(
                name: "ExportLimitMode",
                table: "RoleSettings");

            migrationBuilder.DropColumn(
                name: "ExportLimitOverrideMode",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "ExportLimitRowsOverride",
                table: "AspNetUsers");

            migrationBuilder.AlterColumn<int>(
                name: "ExportLimitRows",
                table: "RoleSettings",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.UpdateData(
                table: "RoleSettings",
                keyColumn: "RoleName",
                keyValue: "Admin",
                column: "ExportLimitRows",
                value: 1000000);

            migrationBuilder.UpdateData(
                table: "RoleSettings",
                keyColumn: "RoleName",
                keyValue: "SuperAdmin",
                column: "ExportLimitRows",
                value: 1000000);
        }
    }
}
