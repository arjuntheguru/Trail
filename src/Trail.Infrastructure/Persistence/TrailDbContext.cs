using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Trail.Application.Common.Abstractions;
using Trail.Application.Common.Events;
using Trail.Domain.Auditing;
using Trail.Domain.Common;
using Trail.Domain.Files;
using Trail.Domain.Identity;
using Trail.Domain.Integrations;
using Trail.Domain.Issues;
using Trail.Domain.Notifications;
using Trail.Domain.Organizations;
using Trail.Domain.Tasks;
using Trail.Infrastructure.Persistence.Conventions;

namespace Trail.Infrastructure.Persistence;

public sealed class TrailDbContext(
    DbContextOptions<TrailDbContext> options, ITenantContext tenant, IDomainEventDispatcher? events = null)
    : DbContext(options), IAppDbContext
{
    /// <summary>Handlers may raise further events; this bounds a runaway chain.</summary>
    private const int MaxEventRounds = 5;

    public DbSet<Organization> Organizations => Set<Organization>();
    public DbSet<Tag> Tags => Set<Tag>();
    public DbSet<Region> Regions => Set<Region>();
    public DbSet<Site> Sites => Set<Site>();
    public DbSet<Department> Departments => Set<Department>();
    public DbSet<MembershipSite> MembershipSites => Set<MembershipSite>();
    public DbSet<MembershipRegion> MembershipRegions => Set<MembershipRegion>();
    public DbSet<MembershipDepartment> MembershipDepartments => Set<MembershipDepartment>();

    public DbSet<TaskTemplate> TaskTemplates => Set<TaskTemplate>();
    public DbSet<TaskTemplateTag> TaskTemplateTags => Set<TaskTemplateTag>();
    public DbSet<TaskSchedule> TaskSchedules => Set<TaskSchedule>();
    public DbSet<TaskScheduleSite> TaskScheduleSites => Set<TaskScheduleSite>();
    public DbSet<TaskScheduleRegion> TaskScheduleRegions => Set<TaskScheduleRegion>();
    public DbSet<SiteTask> SiteTasks => Set<SiteTask>();
    public DbSet<TaskEntry> TaskEntries => Set<TaskEntry>();
    public DbSet<Attachment> Attachments => Set<Attachment>();

    public DbSet<Issue> Issues => Set<Issue>();
    public DbSet<IssueEvent> IssueEvents => Set<IssueEvent>();
    public DbSet<AuditEntry> AuditEntries => Set<AuditEntry>();

    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<NotificationPreference> NotificationPreferences => Set<NotificationPreference>();
    public DbSet<WebhookSubscription> WebhookSubscriptions => Set<WebhookSubscription>();
    public DbSet<WebhookDelivery> WebhookDeliveries => Set<WebhookDelivery>();
    public DbSet<ApiKey> ApiKeys => Set<ApiKey>();
    public DbSet<SensorDevice> SensorDevices => Set<SensorDevice>();

    public DbSet<User> Users => Set<User>();
    public DbSet<Membership> Memberships => Set<Membership>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<UserToken> UserTokens => Set<UserToken>();

    /// <summary>Read by the tenant query filter on every query, so it reflects the tenant at execution time.</summary>
    internal Guid? CurrentOrganizationId => tenant.OrganizationId;

    public override async Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        await DispatchDomainEventsAsync(cancellationToken);
        return await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        DispatchDomainEventsAsync(CancellationToken.None).GetAwaiter().GetResult();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    /// <summary>
    /// Hands the events raised by tracked entities to their handlers before saving, so whatever the handlers add
    /// (notifications, outbox rows) commits atomically with the change and is stamped and audited like it.
    /// Events of a save that then fails are lost with it, which is what makes them safe to act on.
    /// </summary>
    private async Task DispatchDomainEventsAsync(CancellationToken ct)
    {
        for (var round = 0; ; round++)
        {
            var raised = ChangeTracker.Entries<Entity>()
                .Select(e => e.Entity)
                .Where(e => e.DomainEvents.Count > 0)
                .ToList();
            if (raised.Count == 0) return;
            if (round == MaxEventRounds)
                throw new InvalidOperationException("Domain event handlers kept raising events; check for a cycle.");

            var batch = raised.SelectMany(e => e.DomainEvents).OrderBy(e => e.OccurredAt).ToList();
            foreach (var entity in raised) entity.ClearDomainEvents();
            if (events is not null) await events.DispatchAsync(batch, ct);
        }
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.Properties<DateTime>().HaveConversion<UtcDateTimeConverter>();
        configurationBuilder.Properties<DateTime?>().HaveConversion<NullableUtcDateTimeConverter>();
        configurationBuilder.Properties<DateOnly>().HaveConversion<DateOnlyConverter>().HaveColumnType("date");
        configurationBuilder.Properties<TimeOnly>().HaveConversion<TimeOnlyConverter>().HaveColumnType("time");
        configurationBuilder.Properties<decimal>().HavePrecision(18, 4);
        configurationBuilder.Properties<Enum>().HaveConversion<string>().HaveMaxLength(64);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(TrailDbContext).Assembly);
        // Audit rows carry a nullable organization (platform-level rows have none), so they are filtered here
        // rather than through ITenantOwned.
        ApplyAuditFilter(modelBuilder);

        foreach (var entityType in modelBuilder.Model.GetEntityTypes().Where(t => t.BaseType is null).ToList())
        {
            var clrType = entityType.ClrType;

            // Ids are assigned in code (Guid v7) when an entity is constructed. Telling EF they are never generated
            // makes a new child added through a navigation of a tracked parent an insert, not an update of a row
            // that does not exist.
            if (typeof(Entity).IsAssignableFrom(clrType))
            {
                modelBuilder.Entity(clrType).Property(nameof(Entity.Id)).ValueGeneratedNever();
                modelBuilder.Entity(clrType).Ignore(nameof(Entity.DomainEvents));
            }

            if (typeof(IAuditable).IsAssignableFrom(clrType))
                modelBuilder.Entity(clrType).Property(nameof(IAuditable.ConcurrencyStamp)).IsConcurrencyToken();

            if (typeof(ITenantOwned).IsAssignableFrom(clrType))
            {
                Invoke(nameof(ApplyTenantFilter), clrType, modelBuilder);
                modelBuilder.Entity(clrType).HasIndex(nameof(ITenantOwned.OrganizationId));
                modelBuilder.Entity(clrType)
                    .HasOne(typeof(Organization))
                    .WithMany()
                    .HasForeignKey(nameof(ITenantOwned.OrganizationId))
                    .OnDelete(DeleteBehavior.Restrict);
            }

            if (typeof(ISoftDeletable).IsAssignableFrom(clrType))
                Invoke(nameof(ApplySoftDeleteFilter), clrType, modelBuilder);
        }
    }

    private void ApplyAuditFilter(ModelBuilder modelBuilder) =>
        modelBuilder.Entity<AuditEntry>().HasQueryFilter(QueryFilters.Tenant, e => e.OrganizationId == CurrentOrganizationId);

    private void ApplyTenantFilter<TEntity>(ModelBuilder modelBuilder) where TEntity : class, ITenantOwned =>
        modelBuilder.Entity<TEntity>().HasQueryFilter(
            QueryFilters.Tenant, e => e.OrganizationId == CurrentOrganizationId);

    private static void ApplySoftDeleteFilter<TEntity>(ModelBuilder modelBuilder) where TEntity : class, ISoftDeletable =>
        modelBuilder.Entity<TEntity>().HasQueryFilter(QueryFilters.SoftDelete, e => e.DeletedAt == null);

    private void Invoke(string method, Type entityType, ModelBuilder modelBuilder) =>
        typeof(TrailDbContext)
            .GetMethod(method, BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static)!
            .MakeGenericMethod(entityType)
            .Invoke(this, [modelBuilder]);
}
