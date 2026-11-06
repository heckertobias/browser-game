using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BrowserGame.Data.Migrations.Sqlite
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
                type: "BLOB",
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
