using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Chores.Migrations
{
    /// <inheritdoc />
    public partial class DropRedundantHouseholdInviteIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_HouseholdInvites_HouseholdId",
                table: "HouseholdInvites");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_HouseholdInvites_HouseholdId",
                table: "HouseholdInvites",
                column: "HouseholdId");
        }
    }
}
