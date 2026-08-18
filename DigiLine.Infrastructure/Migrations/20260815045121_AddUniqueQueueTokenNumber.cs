using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DigiLine.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddUniqueQueueTokenNumber : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_QueueTokens_SessionId_TokenNumber",
                table: "QueueTokens");

            migrationBuilder.CreateIndex(
                name: "IX_QueueTokens_SessionId_TokenNumber",
                table: "QueueTokens",
                columns: new[] { "SessionId", "TokenNumber" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_QueueTokens_SessionId_TokenNumber",
                table: "QueueTokens");

            migrationBuilder.CreateIndex(
                name: "IX_QueueTokens_SessionId_TokenNumber",
                table: "QueueTokens",
                columns: new[] { "SessionId", "TokenNumber" });
        }
    }
}
