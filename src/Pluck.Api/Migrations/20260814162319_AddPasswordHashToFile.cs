using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pluck.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddPasswordHashToFile : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PasswordHash",
                table: "Files",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PasswordHash",
                table: "Files");
        }
    }
}
