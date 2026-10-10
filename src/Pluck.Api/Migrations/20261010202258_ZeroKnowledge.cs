using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pluck.Api.Migrations
{
    /// <inheritdoc />
    public partial class ZeroKnowledge : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PasswordHash",
                table: "Files");

            migrationBuilder.AddColumn<string>(
                name: "AuthHash",
                table: "Files",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "IV",
                table: "Files",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Salt",
                table: "Files",
                type: "TEXT",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AuthHash",
                table: "Files");

            migrationBuilder.DropColumn(
                name: "IV",
                table: "Files");

            migrationBuilder.DropColumn(
                name: "Salt",
                table: "Files");

            migrationBuilder.AddColumn<string>(
                name: "PasswordHash",
                table: "Files",
                type: "TEXT",
                nullable: true);
        }
    }
}
