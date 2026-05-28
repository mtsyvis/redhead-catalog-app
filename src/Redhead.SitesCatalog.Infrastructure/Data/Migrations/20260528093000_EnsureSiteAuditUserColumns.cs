using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Redhead.SitesCatalog.Infrastructure.Data;

#nullable disable

namespace Redhead.SitesCatalog.Infrastructure.Data.Migrations;

/// <inheritdoc />
[DbContext(typeof(ApplicationDbContext))]
[Migration("20260528093000_EnsureSiteAuditUserColumns")]
public partial class EnsureSiteAuditUserColumns : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            "ALTER TABLE \"Sites\" ADD COLUMN IF NOT EXISTS \"CreatedBy\" character varying(320);");

        migrationBuilder.Sql(
            "ALTER TABLE \"Sites\" ADD COLUMN IF NOT EXISTS \"UpdatedBy\" character varying(320);");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
    }
}
