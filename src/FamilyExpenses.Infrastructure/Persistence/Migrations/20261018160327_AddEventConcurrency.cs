using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FamilyExpenses.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddEventConcurrency : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_HouseholdApprovals_EventId",
                table: "HouseholdApprovals");

            migrationBuilder.AddColumn<long>(
                name: "Version",
                table: "Events",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.CreateIndex(
                name: "IX_HouseholdApprovals_EventId_HouseholdId",
                table: "HouseholdApprovals",
                columns: new[] { "EventId", "HouseholdId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_HouseholdApprovals_EventId_HouseholdId",
                table: "HouseholdApprovals");

            migrationBuilder.DropColumn(
                name: "Version",
                table: "Events");

            migrationBuilder.CreateIndex(
                name: "IX_HouseholdApprovals_EventId",
                table: "HouseholdApprovals",
                column: "EventId");
        }
    }
}
