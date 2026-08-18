using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DigiLine.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPhoneAccount : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PhoneAccounts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PhoneNumber = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PhoneAccounts", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PhoneAccounts_PhoneNumber",
                table: "PhoneAccounts",
                column: "PhoneNumber",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PhoneAccounts");
        }
    }
}
