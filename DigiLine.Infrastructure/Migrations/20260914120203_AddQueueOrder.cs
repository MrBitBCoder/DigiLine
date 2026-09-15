using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DigiLine.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddQueueOrder : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "NextQueueOrder",
                table: "Sessions",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<long>(
                name: "QueueOrder",
                table: "QueueTokens",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.Sql("UPDATE QueueTokens SET QueueOrder = TokenNumber;");
            migrationBuilder.Sql("""
                UPDATE Sessions
                SET NextQueueOrder = COALESCE(
                    (SELECT MAX(QueueOrder) + 1 FROM QueueTokens WHERE SessionId = Sessions.Id), 1);
                """);

            migrationBuilder.CreateIndex(
                name: "IX_QueueTokens_SessionId_QueueOrder",
                table: "QueueTokens",
                columns: new[] { "SessionId", "QueueOrder" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_QueueTokens_SessionId_QueueOrder",
                table: "QueueTokens");
            migrationBuilder.DropColumn(
                name: "NextQueueOrder",
                table: "Sessions");

            migrationBuilder.DropColumn(
                name: "QueueOrder",
                table: "QueueTokens");
        }
    }
}
