using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BrowserGame.Data.Migrations.Postgres
{
    /// <inheritdoc />
    public partial class AddKeyBindings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<byte[]>(
                name: "KeyBindings",
                table: "AspNetUsers",
                type: "bytea",
                maxLength: 512,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "KeyBindings",
                table: "AspNetUsers");
        }
    }
}
