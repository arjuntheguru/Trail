using Microsoft.EntityFrameworkCore;
using Trail.Domain.Auditing;
using Trail.Domain.Files;
using Trail.Domain.Identity;
using Trail.Domain.Issues;
using Trail.Domain.Organizations;
using Trail.Domain.Tasks;

namespace Trail.Application.Common.Abstractions;

/// <summary>
/// Unit of work over the application's data. Tenant-owned sets are already filtered to <see cref="ITenantContext"/>
/// and hide soft-deleted rows; use IgnoreQueryFilters only with a named filter and a reason.
/// </summary>
public interface IAppDbContext
{
    DbSet<Organization> Organizations { get; }
    DbSet<Tag> Tags { get; }
    DbSet<Region> Regions { get; }
    DbSet<Site> Sites { get; }
    DbSet<Department> Departments { get; }
    DbSet<MembershipSite> MembershipSites { get; }
    DbSet<MembershipRegion> MembershipRegions { get; }
    DbSet<MembershipDepartment> MembershipDepartments { get; }

    DbSet<TaskTemplate> TaskTemplates { get; }
    DbSet<TaskTemplateTag> TaskTemplateTags { get; }
    DbSet<TaskSchedule> TaskSchedules { get; }
    DbSet<TaskScheduleSite> TaskScheduleSites { get; }
    DbSet<TaskScheduleRegion> TaskScheduleRegions { get; }
    DbSet<SiteTask> SiteTasks { get; }
    DbSet<TaskEntry> TaskEntries { get; }
    DbSet<Attachment> Attachments { get; }

    DbSet<Issue> Issues { get; }
    DbSet<IssueEvent> IssueEvents { get; }
    DbSet<AuditEntry> AuditEntries { get; }

    DbSet<User> Users { get; }
    DbSet<Membership> Memberships { get; }
    DbSet<Role> Roles { get; }
    DbSet<RefreshToken> RefreshTokens { get; }
    DbSet<UserToken> UserTokens { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}

public static class QueryFilters
{
    public const string Tenant = "Tenant";
    public const string SoftDelete = "SoftDelete";
}
