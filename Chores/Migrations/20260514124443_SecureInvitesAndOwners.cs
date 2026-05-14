using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Chores.Migrations
{
    /// <inheritdoc />
    public partial class SecureInvitesAndOwners : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsHouseholdOwner",
                table: "Users",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.Sql("""
                UPDATE Users
                SET IsHouseholdOwner = 1
                WHERE Id IN (
                    SELECT MIN(Id)
                    FROM Users
                    GROUP BY HouseholdId
                );
                """);

            migrationBuilder.CreateTable(
                name: "HouseholdInvites",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    HouseholdId = table.Column<int>(type: "INTEGER", nullable: false),
                    InvitedByUserId = table.Column<int>(type: "INTEGER", nullable: false),
                    LoginName = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    AcceptedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    DeclinedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HouseholdInvites", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HouseholdInvites_Households_HouseholdId",
                        column: x => x.HouseholdId,
                        principalTable: "Households",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_HouseholdInvites_Users_InvitedByUserId",
                        column: x => x.InvitedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_HouseholdInvites_HouseholdId",
                table: "HouseholdInvites",
                column: "HouseholdId");

            migrationBuilder.CreateIndex(
                name: "IX_HouseholdInvites_InvitedByUserId",
                table: "HouseholdInvites",
                column: "InvitedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_HouseholdInvites_LoginName",
                table: "HouseholdInvites",
                column: "LoginName");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "HouseholdInvites");

            migrationBuilder.DropColumn(
                name: "IsHouseholdOwner",
                table: "Users");
        }
    }
}
