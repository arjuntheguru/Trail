using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Trail.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Issues : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "issues",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false),
                    Number = table.Column<int>(type: "int", nullable: false),
                    Title = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "varchar(5000)", maxLength: 5000, nullable: true),
                    SiteId = table.Column<Guid>(type: "char(36)", nullable: false),
                    DepartmentId = table.Column<Guid>(type: "char(36)", nullable: true),
                    Priority = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false),
                    Status = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false),
                    Source = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false),
                    SiteTaskId = table.Column<Guid>(type: "char(36)", nullable: true),
                    TaskEntryId = table.Column<Guid>(type: "char(36)", nullable: true),
                    FieldKey = table.Column<string>(type: "varchar(40)", maxLength: 40, nullable: true),
                    AssigneeId = table.Column<Guid>(type: "char(36)", nullable: true),
                    DueAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    PhotoIds = table.Column<string>(type: "json", nullable: false),
                    ResolvedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    ResolvedById = table.Column<Guid>(type: "char(36)", nullable: true),
                    Resolution = table.Column<string>(type: "varchar(2000)", maxLength: 2000, nullable: true),
                    ClosedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    ClosedById = table.Column<Guid>(type: "char(36)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CreatedById = table.Column<Guid>(type: "char(36)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    UpdatedById = table.Column<Guid>(type: "char(36)", nullable: true),
                    ConcurrencyStamp = table.Column<Guid>(type: "char(36)", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "char(36)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_issues", x => x.Id);
                    table.ForeignKey(
                        name: "FK_issues_departments_DepartmentId",
                        column: x => x.DepartmentId,
                        principalTable: "departments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_issues_memberships_AssigneeId",
                        column: x => x.AssigneeId,
                        principalTable: "memberships",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_issues_organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_issues_site_tasks_SiteTaskId",
                        column: x => x.SiteTaskId,
                        principalTable: "site_tasks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_issues_sites_SiteId",
                        column: x => x.SiteId,
                        principalTable: "sites",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_issues_task_entries_TaskEntryId",
                        column: x => x.TaskEntryId,
                        principalTable: "task_entries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "organization_sequences",
                columns: table => new
                {
                    OrganizationId = table.Column<Guid>(type: "char(36)", nullable: false),
                    Name = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false),
                    Value = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_organization_sequences", x => new { x.OrganizationId, x.Name });
                    table.ForeignKey(
                        name: "FK_organization_sequences_organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "issue_events",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false),
                    IssueId = table.Column<Guid>(type: "char(36)", nullable: false),
                    Kind = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false),
                    From = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true),
                    To = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true),
                    Note = table.Column<string>(type: "varchar(2000)", maxLength: 2000, nullable: true),
                    PhotoIds = table.Column<string>(type: "json", nullable: false),
                    OccurredAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    ActorId = table.Column<Guid>(type: "char(36)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CreatedById = table.Column<Guid>(type: "char(36)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    UpdatedById = table.Column<Guid>(type: "char(36)", nullable: true),
                    ConcurrencyStamp = table.Column<Guid>(type: "char(36)", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "char(36)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_issue_events", x => x.Id);
                    table.ForeignKey(
                        name: "FK_issue_events_issues_IssueId",
                        column: x => x.IssueId,
                        principalTable: "issues",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_issue_events_organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_issue_events_IssueId_OccurredAt",
                table: "issue_events",
                columns: new[] { "IssueId", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_issue_events_OrganizationId",
                table: "issue_events",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_issues_AssigneeId",
                table: "issues",
                column: "AssigneeId");

            migrationBuilder.CreateIndex(
                name: "IX_issues_DepartmentId",
                table: "issues",
                column: "DepartmentId");

            migrationBuilder.CreateIndex(
                name: "IX_issues_OrganizationId",
                table: "issues",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_issues_OrganizationId_Number",
                table: "issues",
                columns: new[] { "OrganizationId", "Number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_issues_SiteId_Status",
                table: "issues",
                columns: new[] { "SiteId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_issues_SiteTaskId_FieldKey",
                table: "issues",
                columns: new[] { "SiteTaskId", "FieldKey" });

            migrationBuilder.CreateIndex(
                name: "IX_issues_TaskEntryId",
                table: "issues",
                column: "TaskEntryId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "issue_events");

            migrationBuilder.DropTable(
                name: "organization_sequences");

            migrationBuilder.DropTable(
                name: "issues");
        }
    }
}
