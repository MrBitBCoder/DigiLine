using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DigiLine.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSessionNextTokenNumber : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "NextTokenNumber",
                table: "Sessions",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "NextTokenNumber",
                table: "Sessions");
        }
    }
}
