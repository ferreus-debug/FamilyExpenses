using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FamilyExpenses.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddEventApprovals : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "Events",
                type: "TEXT",
                maxLength: 20,
                nullable: false,
                defaultValue: "Open");

            // Events marked as settled before approvals existed stay settled.
            migrationBuilder.Sql("UPDATE Events SET Status = 'Settled' WHERE IsSettled = 1;");

            migrationBuilder.DropColumn(
                name: "IsSettled",
                table: "Events");

            migrationBuilder.CreateTable(
                name: "HouseholdApprovals",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    HouseholdId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ApprovedByUserId = table.Column<string>(type: "TEXT", maxLength: 450, nullable: false),
                    ApprovedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    EventId = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HouseholdApprovals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HouseholdApprovals_Events_EventId",
                        column: x => x.EventId,
                        principalTable: "Events",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_HouseholdApprovals_Households_HouseholdId",
                        column: x => x.HouseholdId,
                        principalTable: "Households",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_HouseholdApprovals_EventId",
                table: "HouseholdApprovals",
                column: "EventId");

            migrationBuilder.CreateIndex(
                name: "IX_HouseholdApprovals_HouseholdId",
                table: "HouseholdApprovals",
                column: "HouseholdId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "HouseholdApprovals");

            migrationBuilder.AddColumn<bool>(
                name: "IsSettled",
                table: "Events",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.Sql("UPDATE Events SET IsSettled = 1 WHERE Status <> 'Open';");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "Events");
        }
    }
}
