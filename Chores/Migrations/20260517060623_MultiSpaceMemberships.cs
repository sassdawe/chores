using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Chores.Migrations
{
    /// <inheritdoc />
    public partial class MultiSpaceMemberships : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Users_Households_HouseholdId",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_Users_HouseholdId",
                table: "Users");

            migrationBuilder.CreateTable(
                name: "HouseholdMemberships",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    UserId = table.Column<int>(type: "INTEGER", nullable: false),
                    HouseholdId = table.Column<int>(type: "INTEGER", nullable: false),
                    IsOwner = table.Column<bool>(type: "INTEGER", nullable: false),
                    JoinedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HouseholdMemberships", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HouseholdMemberships_Households_HouseholdId",
                        column: x => x.HouseholdId,
                        principalTable: "Households",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_HouseholdMemberships_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.Sql("""
                INSERT INTO HouseholdMemberships (UserId, HouseholdId, IsOwner, JoinedAtUtc)
                SELECT Id, HouseholdId, IsHouseholdOwner, strftime('%Y-%m-%dT%H:%M:%fZ','now')
                FROM Users
                """);

            migrationBuilder.DropColumn(
                name: "HouseholdId",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "IsHouseholdOwner",
                table: "Users");

            migrationBuilder.CreateIndex(
                name: "IX_HouseholdMemberships_HouseholdId",
                table: "HouseholdMemberships",
                column: "HouseholdId");

            migrationBuilder.CreateIndex(
                name: "IX_HouseholdMemberships_UserId_HouseholdId",
                table: "HouseholdMemberships",
                columns: new[] { "UserId", "HouseholdId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "HouseholdId",
                table: "Users",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "IsHouseholdOwner",
                table: "Users",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.Sql("""
                UPDATE Users
                SET HouseholdId = (
                        SELECT HouseholdId
                        FROM HouseholdMemberships
                        WHERE HouseholdMemberships.UserId = Users.Id
                        ORDER BY IsOwner DESC, Id
                        LIMIT 1
                    ),
                    IsHouseholdOwner = COALESCE((
                        SELECT IsOwner
                        FROM HouseholdMemberships
                        WHERE HouseholdMemberships.UserId = Users.Id
                        ORDER BY IsOwner DESC, Id
                        LIMIT 1
                    ), 0)
                """);

            migrationBuilder.DropTable(
                name: "HouseholdMemberships");

            migrationBuilder.CreateIndex(
                name: "IX_Users_HouseholdId",
                table: "Users",
                column: "HouseholdId");

            migrationBuilder.AddForeignKey(
                name: "FK_Users_Households_HouseholdId",
                table: "Users",
                column: "HouseholdId",
                principalTable: "Households",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
