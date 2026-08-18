using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DigiLine.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddQueueToken : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "QueueTokens",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SessionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TokenNumber = table.Column<int>(type: "int", nullable: false),
                    PatientProfileId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    WalkInName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    PhoneNumber = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CalledAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    StartedConsultationAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QueueTokens", x => x.Id);
                    table.ForeignKey(
                        name: "FK_QueueTokens_PatientProfiles_PatientProfileId",
                        column: x => x.PatientProfileId,
                        principalTable: "PatientProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_QueueTokens_Sessions_SessionId",
                        column: x => x.SessionId,
                        principalTable: "Sessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_QueueTokens_PatientProfileId",
                table: "QueueTokens",
                column: "PatientProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_QueueTokens_SessionId",
                table: "QueueTokens",
                column: "SessionId");

            migrationBuilder.CreateIndex(
                name: "IX_QueueTokens_SessionId_TokenNumber",
                table: "QueueTokens",
                columns: new[] { "SessionId", "TokenNumber" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "QueueTokens");
        }
    }
}
