using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Time.Testing;
using Trail.Application.Common.Abstractions;
using Trail.Infrastructure.Persistence;
using Trail.Infrastructure.Persistence.Interceptors;
using Trail.Infrastructure.Tenancy;

namespace Trail.IntegrationTests.Infrastructure;

/// <summary>Builds DbContexts against a migrated test database as a chosen user, for persistence-level tests.</summary>
public sealed class TestDb(string connectionString)
{
    public FakeTimeProvider Clock { get; } = new(new DateTimeOffset(2026, 1, 15, 9, 30, 0, TimeSpan.Zero));

    public static async Task<TestDb> CreateAsync(MySqlFixture mysql)
    {
        var db = new TestDb(await mysql.CreateDatabaseAsync());
        await using var context = db.Context(TestUser.Anonymous);
        await context.Database.MigrateAsync();
        return db;
    }

    public TrailDbContext Context(TestUser user) => Context(user, new TenantContext(user));

    /// <summary>A context with no user whose tenant was set explicitly, the way background jobs scope themselves.</summary>
    public TrailDbContext ContextFor(Guid organizationId)
    {
        var tenant = new TenantContext(TestUser.Anonymous);
        tenant.UseOrganization(organizationId);
        return Context(TestUser.Anonymous, tenant);
    }

    private TrailDbContext Context(TestUser user, ITenantContext tenant)
    {
        var options = new DbContextOptionsBuilder<TrailDbContext>()
            .UseMySQL(connectionString)
            .AddInterceptors(new AuditingInterceptor(user, tenant, Clock))
            .Options;
        return new TrailDbContext(options, tenant);
    }
}

public sealed record TestUser(Guid? UserId, Guid? OrganizationId, bool IsPlatformAdmin = false, params string[] Permissions)
    : ICurrentUser
{
    public static readonly TestUser Anonymous = new(null, null);

    public static TestUser MemberOf(Guid organizationId, params string[] permissions) =>
        new(Guid.CreateVersion7(), organizationId, false, permissions);

    public Guid? MembershipId => null;

    public bool IsAuthenticated => UserId is not null;

    public bool HasPermission(string permission) => Permissions.Contains(permission);
}
