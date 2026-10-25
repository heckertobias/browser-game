using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BrowserGame.Data.Migrations.Sqlite
{
    /// <inheritdoc />
    public partial class AddAccountLanguage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Language",
                table: "AspNetUsers",
                type: "TEXT",
                maxLength: 8,
                nullable: false,
                defaultValue: "de"); // bestehende Accounts haben sich auf Deutsch registriert (006)
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Language",
                table: "AspNetUsers");
        }
    }
}
