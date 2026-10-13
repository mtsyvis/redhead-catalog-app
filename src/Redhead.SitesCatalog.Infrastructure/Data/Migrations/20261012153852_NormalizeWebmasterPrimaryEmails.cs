using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Redhead.SitesCatalog.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class NormalizeWebmasterPrimaryEmails : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(ReadScript("Up"));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(ReadScript("Down"));
        }

        private static string ReadScript(string direction)
        {
            var name = $"Redhead.SitesCatalog.Infrastructure.Data.Migrations.Sql.NormalizeWebmasterPrimaryEmails.{direction}.sql";
            using var stream = typeof(NormalizeWebmasterPrimaryEmails).Assembly.GetManifestResourceStream(name)
                ?? throw new InvalidOperationException($"Missing embedded migration resource: {name}");
            using var reader = new StreamReader(stream);
            return reader.ReadToEnd();
        }
    }
}
