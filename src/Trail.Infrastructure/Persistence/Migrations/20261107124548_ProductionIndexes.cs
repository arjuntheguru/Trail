using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Trail.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ProductionIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_notifications_EmailStatus",
                table: "notifications");

            migrationBuilder.CreateIndex(
                name: "IX_webhook_deliveries_Status_ClaimedAt",
                table: "webhook_deliveries",
                columns: new[] { "Status", "ClaimedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_site_tasks_OrganizationId_DueDate",
                table: "site_tasks",
                columns: new[] { "OrganizationId", "DueDate" });

            migrationBuilder.CreateIndex(
                name: "IX_notifications_EmailStatus_CreatedAt",
                table: "notifications",
                columns: new[] { "EmailStatus", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_issues_OrganizationId_Status_DueAt",
                table: "issues",
                columns: new[] { "OrganizationId", "Status", "DueAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_webhook_deliveries_Status_ClaimedAt",
                table: "webhook_deliveries");

            migrationBuilder.DropIndex(
                name: "IX_site_tasks_OrganizationId_DueDate",
                table: "site_tasks");

            migrationBuilder.DropIndex(
                name: "IX_notifications_EmailStatus_CreatedAt",
                table: "notifications");

            migrationBuilder.DropIndex(
                name: "IX_issues_OrganizationId_Status_DueAt",
                table: "issues");

            migrationBuilder.CreateIndex(
                name: "IX_notifications_EmailStatus",
                table: "notifications",
                column: "EmailStatus");
        }
    }
}