using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pluck.Api.Migrations
{
    /// <inheritdoc />
    public partial class NormalizeUserNamesToLowercase : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("UPDATE Users SET Name = LOWER(Name);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Lowercasing is irreversible; no rollback needed
        }
    }
}
