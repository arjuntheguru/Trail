using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Trail.Application.Common.Abstractions;
using Trail.Domain.Common;
using Trail.Domain.Identity;
using Trail.Domain.Organizations;
using Trail.Infrastructure.Persistence.Conventions;

namespace Trail.Infrastructure.Persistence;

public sealed class TrailDbContext(DbContextOptions<TrailDbContext> options, ITenantContext tenant)
    : DbContext(options), IAppDbContext
{
    public DbSet<Organization> Organizations => Set<Organization>();
    public DbSet<Tag> Tags => Set<Tag>();
    public DbSet<Region> Regions => Set<Region>();
    public DbSet<Site> Sites => Set<Site>();
    public DbSet<Department> Departments => Set<Department>();
    public DbSet<MembershipSite> MembershipSites => Set<MembershipSite>();
    public DbSet<MembershipRegion> MembershipRegions => Set<MembershipRegion>();
    public DbSet<MembershipDepartment> MembershipDepartments => Set<MembershipDepartment>();

    public DbSet<User> Users => Set<User>();
    public DbSet<Membership> Memberships => Set<Membership>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<UserToken> UserTokens => Set<UserToken>();

    /// <summary>Read by the tenant query filter on every query, so it reflects the tenant at execution time.</summary>
    internal Guid? CurrentOrganizationId => tenant.OrganizationId;

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.Properties<DateTime>().HaveConversion<UtcDateTimeConverter>();
        configurationBuilder.Properties<DateTime?>().HaveConversion<NullableUtcDateTimeConverter>();
        configurationBuilder.Properties<decimal>().HavePrecision(18, 4);
        configurationBuilder.Properties<Enum>().HaveConversion<string>().HaveMaxLength(64);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(TrailDbContext).Assembly);

        foreach (var entityType in modelBuilder.Model.GetEntityTypes().Where(t => t.BaseType is null).ToList())
        {
            var clrType = entityType.ClrType;

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
