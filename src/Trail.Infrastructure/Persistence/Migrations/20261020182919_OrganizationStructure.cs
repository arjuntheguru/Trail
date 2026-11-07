using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Trail.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class OrganizationStructure : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "departments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false),
                    Name = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false),
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
                    table.PrimaryKey("PK_departments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_departments_organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "regions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false),
                    Name = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true),
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
                    table.PrimaryKey("PK_regions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_regions_organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "membership_departments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false),
                    MembershipId = table.Column<Guid>(type: "char(36)", nullable: false),
                    DepartmentId = table.Column<Guid>(type: "char(36)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CreatedById = table.Column<Guid>(type: "char(36)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    UpdatedById = table.Column<Guid>(type: "char(36)", nullable: true),
                    ConcurrencyStamp = table.Column<Guid>(type: "char(36)", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "char(36)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_membership_departments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_membership_departments_departments_DepartmentId",
                        column: x => x.DepartmentId,
                        principalTable: "departments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_membership_departments_memberships_MembershipId",
                        column: x => x.MembershipId,
                        principalTable: "memberships",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_membership_departments_organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "membership_regions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false),
                    MembershipId = table.Column<Guid>(type: "char(36)", nullable: false),
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
                    table.PrimaryKey("PK_membership_regions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_membership_regions_memberships_MembershipId",
                        column: x => x.MembershipId,
                        principalTable: "memberships",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_membership_regions_organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_membership_regions_regions_RegionId",
                        column: x => x.RegionId,
                        principalTable: "regions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "sites",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false),
                    Name = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false),
                    Code = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: true),
                    RegionId = table.Column<Guid>(type: "char(36)", nullable: true),
                    TimeZone = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false),
                    Address = table.Column<string>(type: "json", nullable: true),
                    Phone = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: true),
                    Email = table.Column<string>(type: "varchar(254)", maxLength: 254, nullable: true),
                    IsActive = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    OpeningHours = table.Column<string>(type: "json", nullable: false),
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
                    table.PrimaryKey("PK_sites", x => x.Id);
                    table.ForeignKey(
                        name: "FK_sites_organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_sites_regions_RegionId",
                        column: x => x.RegionId,
                        principalTable: "regions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "membership_sites",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false),
                    MembershipId = table.Column<Guid>(type: "char(36)", nullable: false),
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
                    table.PrimaryKey("PK_membership_sites", x => x.Id);
                    table.ForeignKey(
                        name: "FK_membership_sites_memberships_MembershipId",
                        column: x => x.MembershipId,
                        principalTable: "memberships",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_membership_sites_organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_membership_sites_sites_SiteId",
                        column: x => x.SiteId,
                        principalTable: "sites",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_departments_OrganizationId",
                table: "departments",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_membership_departments_DepartmentId",
                table: "membership_departments",
                column: "DepartmentId");

            migrationBuilder.CreateIndex(
                name: "IX_membership_departments_MembershipId_DepartmentId",
                table: "membership_departments",
                columns: new[] { "MembershipId", "DepartmentId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_membership_departments_OrganizationId",
                table: "membership_departments",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_membership_regions_MembershipId_RegionId",
                table: "membership_regions",
                columns: new[] { "MembershipId", "RegionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_membership_regions_OrganizationId",
                table: "membership_regions",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_membership_regions_RegionId",
                table: "membership_regions",
                column: "RegionId");

            migrationBuilder.CreateIndex(
                name: "IX_membership_sites_MembershipId_SiteId",
                table: "membership_sites",
                columns: new[] { "MembershipId", "SiteId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_membership_sites_OrganizationId",
                table: "membership_sites",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_membership_sites_SiteId",
                table: "membership_sites",
                column: "SiteId");

            migrationBuilder.CreateIndex(
                name: "IX_regions_OrganizationId",
                table: "regions",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_sites_OrganizationId",
                table: "sites",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_sites_OrganizationId_Name",
                table: "sites",
                columns: new[] { "OrganizationId", "Name" });

            migrationBuilder.CreateIndex(
                name: "IX_sites_RegionId",
                table: "sites",
                column: "RegionId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "membership_departments");

            migrationBuilder.DropTable(
                name: "membership_regions");

            migrationBuilder.DropTable(
                name: "membership_sites");

            migrationBuilder.DropTable(
                name: "departments");

            migrationBuilder.DropTable(
                name: "sites");

            migrationBuilder.DropTable(
                name: "regions");
        }
    }
}
