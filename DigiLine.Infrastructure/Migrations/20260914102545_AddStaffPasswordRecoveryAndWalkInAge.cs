using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DigiLine.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddStaffPasswordRecoveryAndWalkInAge : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "NormalizedEmail",
                table: "Staff",
                type: "nvarchar(320)",
                maxLength: 320,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<Guid>(
                name: "PasswordVersion",
                table: "Staff",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<int>(
                name: "WalkInAge",
                table: "QueueTokens",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "PasswordResetTokens",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StaffId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TokenHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UsedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PasswordResetTokens", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PasswordResetTokens_Staff_StaffId",
                        column: x => x.StaffId,
                        principalTable: "Staff",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.Sql("""
                IF EXISTS (
                    SELECT 1 FROM Staff
                    GROUP BY UPPER(LTRIM(RTRIM(Email)))
                    HAVING COUNT(*) > 1
                )
                    THROW 51000, 'Duplicate normalized staff emails found. Run the collision report and resolve them before migration.', 1;
                UPDATE Staff SET Email = LTRIM(RTRIM(Email)),
                    NormalizedEmail = UPPER(LTRIM(RTRIM(Email))), PasswordVersion = NEWID();
                """);

            migrationBuilder.CreateIndex(
                name: "IX_Staff_NormalizedEmail",
                table: "Staff",
                column: "NormalizedEmail",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PasswordResetTokens_StaffId_CreatedAt",
                table: "PasswordResetTokens",
                columns: new[] { "StaffId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_PasswordResetTokens_TokenHash",
                table: "PasswordResetTokens",
                column: "TokenHash",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PasswordResetTokens");

            migrationBuilder.DropIndex(
                name: "IX_Staff_NormalizedEmail",
                table: "Staff");

            migrationBuilder.DropColumn(
                name: "NormalizedEmail",
                table: "Staff");

            migrationBuilder.DropColumn(
                name: "PasswordVersion",
                table: "Staff");

            migrationBuilder.DropColumn(
                name: "WalkInAge",
                table: "QueueTokens");
        }
    }
}
