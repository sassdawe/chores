using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Chores.Migrations
{
    /// <inheritdoc />
    public partial class AddSkippedToCompletionRecord : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsSkipped",
                table: "CompletionRecords",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsSkipped",
                table: "CompletionRecords");
        }
    }
}
