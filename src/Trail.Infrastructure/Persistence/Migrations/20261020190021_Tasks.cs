using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Trail.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Tasks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "attachments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false),
                    Kind = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false),
                    FileName = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: false),
                    ContentType = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    StorageKey = table.Column<string>(type: "varchar(300)", maxLength: 300, nullable: false),
                    Sha256 = table.Column<string>(type: "char(64)", fixedLength: true, maxLength: 64, nullable: false),
                    SiteId = table.Column<Guid>(type: "char(36)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CreatedById = table.Column<Guid>(type: "char(36)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    UpdatedById = table.Column<Guid>(type: "char(36)", nullable: true),
                    ConcurrencyStamp = table.Column<Guid>(type: "char(36)", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "char(36)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_attachments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_attachments_organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_attachments_sites_SiteId",
                        column: x => x.SiteId,
                        principalTable: "sites",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "task_templates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false),
                    Name = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "varchar(2000)", maxLength: 2000, nullable: true),
                    Kind = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false),
                    Fields = table.Column<string>(type: "json", nullable: false),
                    RequiresApproval = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    RequiresPhoto = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    LibraryKey = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    DeletedById = table.Column<Guid>(type: "char(36)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CreatedById = table.Column<Guid>(type: "char(36)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    UpdatedById = table.Column<Guid>(type: "char(36)", nullable: true),
                    ConcurrencyStamp = table.Column<Guid>(type: "char(36)", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "char(36)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_task_templates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_task_templates_organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "task_schedules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false),
                    TemplateId = table.Column<Guid>(type: "char(36)", nullable: false),
                    Frequency = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false),
                    Interval = table.Column<int>(type: "int", nullable: false),
                    StartDate = table.Column<DateTime>(type: "date", nullable: false),
                    EndDate = table.Column<DateTime>(type: "date", nullable: true),
                    Days = table.Column<int>(type: "int", maxLength: 64, nullable: false),
                    DayOfMonth = table.Column<int>(type: "int", nullable: true),
                    DueTime = table.Column<TimeSpan>(type: "time", nullable: false),
                    AvailableFrom = table.Column<TimeSpan>(type: "time", nullable: true),
                    AllSites = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    DepartmentId = table.Column<Guid>(type: "char(36)", nullable: true),
                    IsActive = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    DeletedById = table.Column<Guid>(type: "char(36)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CreatedById = table.Column<Guid>(type: "char(36)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    UpdatedById = table.Column<Guid>(type: "char(36)", nullable: true),
                    ConcurrencyStamp = table.Column<Guid>(type: "char(36)", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "char(36)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_task_schedules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_task_schedules_departments_DepartmentId",
                        column: x => x.DepartmentId,
                        principalTable: "departments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_task_schedules_organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_task_schedules_task_templates_TemplateId",
                        column: x => x.TemplateId,
                        principalTable: "task_templates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "task_template_tags",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false),
                    TemplateId = table.Column<Guid>(type: "char(36)", nullable: false),
                    TagId = table.Column<Guid>(type: "char(36)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CreatedById = table.Column<Guid>(type: "char(36)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    UpdatedById = table.Column<Guid>(type: "char(36)", nullable: true),
                    ConcurrencyStamp = table.Column<Guid>(type: "char(36)", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "char(36)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_task_template_tags", x => x.Id);
                    table.ForeignKey(
                        name: "FK_task_template_tags_organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_task_template_tags_tags_TagId",
                        column: x => x.TagId,
                        principalTable: "tags",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_task_template_tags_task_templates_TemplateId",
                        column: x => x.TemplateId,
                        principalTable: "task_templates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "site_tasks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false),
                    SiteId = table.Column<Guid>(type: "char(36)", nullable: false),
                    TemplateId = table.Column<Guid>(type: "char(36)", nullable: false),
                    TemplateVersion = table.Column<int>(type: "int", nullable: false),
                    ScheduleId = table.Column<Guid>(type: "char(36)", nullable: true),
                    DepartmentId = table.Column<Guid>(type: "char(36)", nullable: true),
                    Source = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false),
                    Title = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "varchar(2000)", maxLength: 2000, nullable: true),
                    Kind = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false),
                    Fields = table.Column<string>(type: "json", nullable: false),
                    RequiresApproval = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    RequiresPhoto = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    ScheduledDate = table.Column<DateTime>(type: "date", nullable: false),
                    DueDate = table.Column<DateTime>(type: "date", nullable: false),
                    DueAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    AvailableAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    Status = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false),
                    SnoozeCount = table.Column<int>(type: "int", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    CompletedById = table.Column<Guid>(type: "char(36)", nullable: true),
                    CompletedLate = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    HasFailures = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    ApprovedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    ApprovedById = table.Column<Guid>(type: "char(36)", nullable: true),
                    ApprovalNote = table.Column<string>(type: "varchar(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CreatedById = table.Column<Guid>(type: "char(36)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    UpdatedById = table.Column<Guid>(type: "char(36)", nullable: true),
                    ConcurrencyStamp = table.Column<Guid>(type: "char(36)", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "char(36)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_site_tasks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_site_tasks_departments_DepartmentId",
                        column: x => x.DepartmentId,
                        principalTable: "departments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_site_tasks_organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_site_tasks_sites_SiteId",
                        column: x => x.SiteId,
                        principalTable: "sites",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_site_tasks_task_schedules_ScheduleId",
                        column: x => x.ScheduleId,
                        principalTable: "task_schedules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_site_tasks_task_templates_TemplateId",
                        column: x => x.TemplateId,
                        principalTable: "task_templates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "task_schedule_regions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false),
                    ScheduleId = table.Column<Guid>(type: "char(36)", nullable: false),
                    RegionId = table.Column<Guid>(type: "char(36)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CreatedById = table.Column<Guid>(type: "char(36)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    UpdatedById = table.Column<Guid>(type: "char(36)", nullable: true),
                    ConcurrencyStamp = table.Column<Guid>(type: "char(36)", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "char(36)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_task_schedule_regions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_task_schedule_regions_organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_task_schedule_regions_regions_RegionId",
                        column: x => x.RegionId,
                        principalTable: "regions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_task_schedule_regions_task_schedules_ScheduleId",
                        column: x => x.ScheduleId,
                        principalTable: "task_schedules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "task_schedule_sites",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false),
                    ScheduleId = table.Column<Guid>(type: "char(36)", nullable: false),
                    SiteId = table.Column<Guid>(type: "char(36)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CreatedById = table.Column<Guid>(type: "char(36)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    UpdatedById = table.Column<Guid>(type: "char(36)", nullable: true),
                    ConcurrencyStamp = table.Column<Guid>(type: "char(36)", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "char(36)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_task_schedule_sites", x => x.Id);
                    table.ForeignKey(
                        name: "FK_task_schedule_sites_organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_task_schedule_sites_sites_SiteId",
                        column: x => x.SiteId,
                        principalTable: "sites",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_task_schedule_sites_task_schedules_ScheduleId",
                        column: x => x.ScheduleId,
                        principalTable: "task_schedules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "task_entries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false),
                    SiteTaskId = table.Column<Guid>(type: "char(36)", nullable: false),
                    Answers = table.Column<string>(type: "json", nullable: false),
                    PhotoIds = table.Column<string>(type: "json", nullable: false),
                    Note = table.Column<string>(type: "varchar(2000)", maxLength: 2000, nullable: true),
                    Failures = table.Column<string>(type: "json", nullable: false),
                    SubmittedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    SubmittedById = table.Column<Guid>(type: "char(36)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CreatedById = table.Column<Guid>(type: "char(36)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    UpdatedById = table.Column<Guid>(type: "char(36)", nullable: true),
                    ConcurrencyStamp = table.Column<Guid>(type: "char(36)", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "char(36)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_task_entries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_task_entries_organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_task_entries_site_tasks_SiteTaskId",
                        column: x => x.SiteTaskId,
                        principalTable: "site_tasks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_attachments_OrganizationId",
                table: "attachments",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_attachments_SiteId",
                table: "attachments",
                column: "SiteId");

            migrationBuilder.CreateIndex(
                name: "IX_site_tasks_DepartmentId",
                table: "site_tasks",
                column: "DepartmentId");

            migrationBuilder.CreateIndex(
                name: "IX_site_tasks_OrganizationId",
                table: "site_tasks",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_site_tasks_ScheduleId_SiteId_ScheduledDate",
                table: "site_tasks",
                columns: new[] { "ScheduleId", "SiteId", "ScheduledDate" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_site_tasks_SiteId_DueDate",
                table: "site_tasks",
                columns: new[] { "SiteId", "DueDate" });

            migrationBuilder.CreateIndex(
                name: "IX_site_tasks_Status_DueAt",
                table: "site_tasks",
                columns: new[] { "Status", "DueAt" });

            migrationBuilder.CreateIndex(
                name: "IX_site_tasks_TemplateId",
                table: "site_tasks",
                column: "TemplateId");

            migrationBuilder.CreateIndex(
                name: "IX_task_entries_OrganizationId",
                table: "task_entries",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_task_entries_SiteTaskId",
                table: "task_entries",
                column: "SiteTaskId");

            migrationBuilder.CreateIndex(
                name: "IX_task_schedule_regions_OrganizationId",
                table: "task_schedule_regions",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_task_schedule_regions_RegionId",
                table: "task_schedule_regions",
                column: "RegionId");

            migrationBuilder.CreateIndex(
                name: "IX_task_schedule_regions_ScheduleId_RegionId",
                table: "task_schedule_regions",
                columns: new[] { "ScheduleId", "RegionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_task_schedule_sites_OrganizationId",
                table: "task_schedule_sites",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_task_schedule_sites_ScheduleId_SiteId",
                table: "task_schedule_sites",
                columns: new[] { "ScheduleId", "SiteId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_task_schedule_sites_SiteId",
                table: "task_schedule_sites",
                column: "SiteId");

            migrationBuilder.CreateIndex(
                name: "IX_task_schedules_DepartmentId",
                table: "task_schedules",
                column: "DepartmentId");

            migrationBuilder.CreateIndex(
                name: "IX_task_schedules_OrganizationId",
                table: "task_schedules",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_task_schedules_TemplateId",
                table: "task_schedules",
                column: "TemplateId");

            migrationBuilder.CreateIndex(
                name: "IX_task_template_tags_OrganizationId",
                table: "task_template_tags",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_task_template_tags_TagId",
                table: "task_template_tags",
                column: "TagId");

            migrationBuilder.CreateIndex(
                name: "IX_task_template_tags_TemplateId_TagId",
                table: "task_template_tags",
                columns: new[] { "TemplateId", "TagId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_task_templates_OrganizationId",
                table: "task_templates",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_task_templates_OrganizationId_Name",
                table: "task_templates",
                columns: new[] { "OrganizationId", "Name" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "attachments");

            migrationBuilder.DropTable(
                name: "task_entries");

            migrationBuilder.DropTable(
                name: "task_schedule_regions");

            migrationBuilder.DropTable(
                name: "task_schedule_sites");

            migrationBuilder.DropTable(
                name: "task_template_tags");

            migrationBuilder.DropTable(
                name: "site_tasks");

            migrationBuilder.DropTable(
                name: "task_schedules");

            migrationBuilder.DropTable(
                name: "task_templates");
        }
    }
}
