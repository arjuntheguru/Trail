using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Trail.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class NotificationsAndIntegrations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "EscalatedAt",
                table: "issues",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "OverdueNotifiedAt",
                table: "issues",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "api_keys",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false),
                    Name = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false),
                    Prefix = table.Column<string>(type: "varchar(8)", maxLength: 8, nullable: false),
                    KeyHash = table.Column<string>(type: "char(64)", fixedLength: true, maxLength: 64, nullable: false),
                    Scopes = table.Column<string>(type: "json", nullable: false),
                    SiteIds = table.Column<string>(type: "json", nullable: true),
                    ExpiresAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    LastUsedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    RevokedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
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
                    table.PrimaryKey("PK_api_keys", x => x.Id);
                    table.ForeignKey(
                        name: "FK_api_keys_organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "notification_preferences",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false),
                    MembershipId = table.Column<Guid>(type: "char(36)", nullable: false),
                    EmailKinds = table.Column<string>(type: "json", nullable: false),
                    DigestEnabled = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    DigestHour = table.Column<int>(type: "int", nullable: false),
                    LastDigestOn = table.Column<DateTime>(type: "date", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CreatedById = table.Column<Guid>(type: "char(36)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    UpdatedById = table.Column<Guid>(type: "char(36)", nullable: true),
                    ConcurrencyStamp = table.Column<Guid>(type: "char(36)", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "char(36)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_notification_preferences", x => x.Id);
                    table.ForeignKey(
                        name: "FK_notification_preferences_memberships_MembershipId",
                        column: x => x.MembershipId,
                        principalTable: "memberships",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_notification_preferences_organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "notifications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false),
                    MembershipId = table.Column<Guid>(type: "char(36)", nullable: false),
                    Kind = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false),
                    Title = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false),
                    Body = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: false),
                    SubjectType = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false),
                    SubjectId = table.Column<Guid>(type: "char(36)", nullable: false),
                    SiteId = table.Column<Guid>(type: "char(36)", nullable: true),
                    ReadAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    EmailStatus = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false),
                    EmailAttempts = table.Column<int>(type: "int", nullable: false),
                    EmailSentAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    EmailClaimToken = table.Column<Guid>(type: "char(36)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CreatedById = table.Column<Guid>(type: "char(36)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    UpdatedById = table.Column<Guid>(type: "char(36)", nullable: true),
                    ConcurrencyStamp = table.Column<Guid>(type: "char(36)", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "char(36)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_notifications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_notifications_memberships_MembershipId",
                        column: x => x.MembershipId,
                        principalTable: "memberships",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_notifications_organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_notifications_sites_SiteId",
                        column: x => x.SiteId,
                        principalTable: "sites",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "sensor_devices",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false),
                    ExternalId = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false),
                    Name = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false),
                    SiteId = table.Column<Guid>(type: "char(36)", nullable: false),
                    TemplateId = table.Column<Guid>(type: "char(36)", nullable: false),
                    FieldKey = table.Column<string>(type: "varchar(40)", maxLength: 40, nullable: false),
                    LastReadingAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    LastValue = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: true),
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
                    table.PrimaryKey("PK_sensor_devices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_sensor_devices_organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_sensor_devices_sites_SiteId",
                        column: x => x.SiteId,
                        principalTable: "sites",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_sensor_devices_task_templates_TemplateId",
                        column: x => x.TemplateId,
                        principalTable: "task_templates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "webhook_subscriptions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false),
                    Url = table.Column<string>(type: "varchar(2000)", maxLength: 2000, nullable: false),
                    Description = table.Column<string>(type: "varchar(300)", maxLength: 300, nullable: true),
                    EventTypes = table.Column<string>(type: "json", nullable: false),
                    Secret = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false),
                    IsActive = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    FailureCount = table.Column<int>(type: "int", nullable: false),
                    DisabledAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    LastSuccessAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    LastFailureAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
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
                    table.PrimaryKey("PK_webhook_subscriptions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_webhook_subscriptions_organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "webhook_deliveries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false),
                    SubscriptionId = table.Column<Guid>(type: "char(36)", nullable: false),
                    EventId = table.Column<Guid>(type: "char(36)", nullable: false),
                    EventType = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false),
                    Payload = table.Column<string>(type: "longtext", nullable: false),
                    Status = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false),
                    Attempts = table.Column<int>(type: "int", nullable: false),
                    NextAttemptAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    ClaimToken = table.Column<Guid>(type: "char(36)", nullable: true),
                    ClaimedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    LastStatusCode = table.Column<int>(type: "int", nullable: true),
                    LastError = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: true),
                    DeliveredAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CreatedById = table.Column<Guid>(type: "char(36)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    UpdatedById = table.Column<Guid>(type: "char(36)", nullable: true),
                    ConcurrencyStamp = table.Column<Guid>(type: "char(36)", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "char(36)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_webhook_deliveries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_webhook_deliveries_organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_webhook_deliveries_webhook_subscriptions_SubscriptionId",
                        column: x => x.SubscriptionId,
                        principalTable: "webhook_subscriptions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_api_keys_KeyHash",
                table: "api_keys",
                column: "KeyHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_api_keys_OrganizationId",
                table: "api_keys",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_api_keys_Prefix",
                table: "api_keys",
                column: "Prefix");

            migrationBuilder.CreateIndex(
                name: "IX_notification_preferences_MembershipId",
                table: "notification_preferences",
                column: "MembershipId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_notification_preferences_OrganizationId",
                table: "notification_preferences",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_notifications_EmailClaimToken",
                table: "notifications",
                column: "EmailClaimToken");

            migrationBuilder.CreateIndex(
                name: "IX_notifications_EmailStatus",
                table: "notifications",
                column: "EmailStatus");

            migrationBuilder.CreateIndex(
                name: "IX_notifications_MembershipId_ReadAt_CreatedAt",
                table: "notifications",
                columns: new[] { "MembershipId", "ReadAt", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_notifications_OrganizationId",
                table: "notifications",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_notifications_SiteId",
                table: "notifications",
                column: "SiteId");

            migrationBuilder.CreateIndex(
                name: "IX_notifications_SubjectType_SubjectId",
                table: "notifications",
                columns: new[] { "SubjectType", "SubjectId" });

            migrationBuilder.CreateIndex(
                name: "IX_sensor_devices_OrganizationId",
                table: "sensor_devices",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_sensor_devices_OrganizationId_ExternalId",
                table: "sensor_devices",
                columns: new[] { "OrganizationId", "ExternalId" });

            migrationBuilder.CreateIndex(
                name: "IX_sensor_devices_SiteId",
                table: "sensor_devices",
                column: "SiteId");

            migrationBuilder.CreateIndex(
                name: "IX_sensor_devices_TemplateId",
                table: "sensor_devices",
                column: "TemplateId");

            migrationBuilder.CreateIndex(
                name: "IX_webhook_deliveries_OrganizationId",
                table: "webhook_deliveries",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_webhook_deliveries_Status_NextAttemptAt",
                table: "webhook_deliveries",
                columns: new[] { "Status", "NextAttemptAt" });

            migrationBuilder.CreateIndex(
                name: "IX_webhook_deliveries_SubscriptionId_CreatedAt",
                table: "webhook_deliveries",
                columns: new[] { "SubscriptionId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_webhook_deliveries_SubscriptionId_EventId",
                table: "webhook_deliveries",
                columns: new[] { "SubscriptionId", "EventId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_webhook_subscriptions_OrganizationId",
                table: "webhook_subscriptions",
                column: "OrganizationId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "api_keys");

            migrationBuilder.DropTable(
                name: "notification_preferences");

            migrationBuilder.DropTable(
                name: "notifications");

            migrationBuilder.DropTable(
                name: "sensor_devices");

            migrationBuilder.DropTable(
                name: "webhook_deliveries");

            migrationBuilder.DropTable(
                name: "webhook_subscriptions");

            migrationBuilder.DropColumn(
                name: "EscalatedAt",
                table: "issues");

            migrationBuilder.DropColumn(
                name: "OverdueNotifiedAt",
                table: "issues");
        }
    }
}
