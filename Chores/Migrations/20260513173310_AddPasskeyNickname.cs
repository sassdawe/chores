using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Chores.Migrations
{
    /// <inheritdoc />
    public partial class AddPasskeyNickname : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Nickname",
                table: "FidoCredentials",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Nickname",
                table: "FidoCredentials");
        }
    }
}
