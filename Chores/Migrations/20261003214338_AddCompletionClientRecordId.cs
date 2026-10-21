using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Chores.Migrations
{
    /// <inheritdoc />
    public partial class AddCompletionClientRecordId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ClientRecordId",
                table: "CompletionRecords",
                type: "TEXT",
                maxLength: 32,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_CompletionRecords_ClientRecordId",
                table: "CompletionRecords",
                column: "ClientRecordId",
                unique: true,
                filter: "ClientRecordId IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_CompletionRecords_ClientRecordId",
                table: "CompletionRecords");

            migrationBuilder.DropColumn(
                name: "ClientRecordId",
                table: "CompletionRecords");
        }
    }
}
