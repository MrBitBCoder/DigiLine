using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DigiLine.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddClinicAdministrationAndSessionCompletion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "InvitationPending",
                table: "Staff",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "Staff",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "BookingStoppedAt",
                table: "Sessions",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CompletedAt",
                table: "Sessions",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "FinalizedAt",
                table: "QueueTokens",
                type: "datetime2",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AdministrationAudits",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClinicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ActorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StaffId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Action = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    OccurredAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AdministrationAudits", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "StaffInvitations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StaffId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TokenHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UsedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeliveryFailed = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StaffInvitations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StaffInvitations_Staff_StaffId",
                        column: x => x.StaffId,
                        principalTable: "Staff",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "StaffRoleAssignment",
                columns: table => new
                {
                    StaffId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Role = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StaffRoleAssignment", x => new { x.StaffId, x.Role });
                    table.ForeignKey(
                        name: "FK_StaffRoleAssignment_Staff_StaffId",
                        column: x => x.StaffId,
                        principalTable: "Staff",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.Sql("INSERT INTO StaffRoleAssignment (StaffId, Role) SELECT Id, Role FROM Staff;");

            migrationBuilder.CreateIndex(
                name: "IX_Sessions_ClinicId_DoctorId_StartTime_Id",
                table: "Sessions",
                columns: new[] { "ClinicId", "DoctorId", "StartTime", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_Sessions_ClinicId_StartTime_Id",
                table: "Sessions",
                columns: new[] { "ClinicId", "StartTime", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_QueueTokens_SessionId_Status_QueueOrder_TokenNumber",
                table: "QueueTokens",
                columns: new[] { "SessionId", "Status", "QueueOrder", "TokenNumber" });

            migrationBuilder.CreateIndex(
                name: "IX_AdministrationAudits_ClinicId_OccurredAt",
                table: "AdministrationAudits",
                columns: new[] { "ClinicId", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_StaffInvitations_StaffId",
                table: "StaffInvitations",
                column: "StaffId");

            migrationBuilder.CreateIndex(
                name: "IX_StaffInvitations_TokenHash",
                table: "StaffInvitations",
                column: "TokenHash",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AdministrationAudits");

            migrationBuilder.DropTable(
                name: "StaffInvitations");

            migrationBuilder.DropTable(
                name: "StaffRoleAssignment");

            migrationBuilder.DropIndex(
                name: "IX_Sessions_ClinicId_DoctorId_StartTime_Id",
                table: "Sessions");

            migrationBuilder.DropIndex(
                name: "IX_Sessions_ClinicId_StartTime_Id",
                table: "Sessions");

            migrationBuilder.DropIndex(
                name: "IX_QueueTokens_SessionId_Status_QueueOrder_TokenNumber",
                table: "QueueTokens");

            migrationBuilder.DropColumn(
                name: "InvitationPending",
                table: "Staff");

            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "Staff");

            migrationBuilder.DropColumn(
                name: "BookingStoppedAt",
                table: "Sessions");

            migrationBuilder.DropColumn(
                name: "CompletedAt",
                table: "Sessions");

            migrationBuilder.DropColumn(
                name: "FinalizedAt",
                table: "QueueTokens");
        }
    }
}
