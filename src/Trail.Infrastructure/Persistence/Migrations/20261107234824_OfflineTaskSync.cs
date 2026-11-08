using System;

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Trail.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class OfflineTaskSync : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "RequestId",
                table: "task_entries",
                type: "char(36)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "SyncedAt",
                table: "task_entries",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CompletionRequestId",
                table: "site_tasks",
                type: "char(36)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "SyncedAt",
                table: "site_tasks",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "RequestId",
                table: "attachments",
                type: "char(36)",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_task_entries_SiteTaskId_RequestId",
                table: "task_entries",
                columns: new[] { "SiteTaskId", "RequestId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_attachments_OrganizationId_CreatedById_RequestId",
                table: "attachments",
                columns: new[] { "OrganizationId", "CreatedById", "RequestId" },
                unique: true);

            // After the composite index exists, so the foreign key always has an index to use.
            migrationBuilder.DropIndex(
                name: "IX_task_entries_SiteTaskId",
                table: "task_entries");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_task_entries_SiteTaskId",
                table: "task_entries",
                column: "SiteTaskId");

            migrationBuilder.DropIndex(
                name: "IX_task_entries_SiteTaskId_RequestId",
                table: "task_entries");

            migrationBuilder.DropIndex(
                name: "IX_attachments_OrganizationId_CreatedById_RequestId",
                table: "attachments");

            migrationBuilder.DropColumn(
                name: "RequestId",
                table: "task_entries");

            migrationBuilder.DropColumn(
                name: "SyncedAt",
                table: "task_entries");

            migrationBuilder.DropColumn(
                name: "CompletionRequestId",
                table: "site_tasks");

            migrationBuilder.DropColumn(
                name: "SyncedAt",
                table: "site_tasks");

            migrationBuilder.DropColumn(
                name: "RequestId",
                table: "attachments");
        }
    }
}