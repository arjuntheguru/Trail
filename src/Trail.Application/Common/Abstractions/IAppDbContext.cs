using Microsoft.EntityFrameworkCore;
using Trail.Domain.Organizations;

namespace Trail.Application.Common.Abstractions;

/// <summary>
/// Unit of work over the application's data. Tenant-owned sets are already filtered to <see cref="ITenantContext"/>
/// and hide soft-deleted rows; use IgnoreQueryFilters only with a named filter and a reason.
/// </summary>
public interface IAppDbContext
{
    DbSet<Organization> Organizations { get; }
    DbSet<Tag> Tags { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}

public static class QueryFilters
{
    public const string Tenant = "Tenant";
    public const string SoftDelete = "SoftDelete";
}
