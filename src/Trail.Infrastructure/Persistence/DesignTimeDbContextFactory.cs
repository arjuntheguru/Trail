using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Trail.Application.Common.Abstractions;

namespace Trail.Infrastructure.Persistence;

/// <summary>Used only by <c>dotnet ef</c>. Set TRAIL_DESIGN_CONNECTION to point at a different server.</summary>
internal sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<TrailDbContext>
{
    private const string LocalDevConnection = "Server=localhost;Port=3317;Database=trail;User=trail;Password=trail-dev;";

    public TrailDbContext CreateDbContext(string[] args)
    {
        var connection = Environment.GetEnvironmentVariable("TRAIL_DESIGN_CONNECTION") ?? LocalDevConnection;
        var options = new DbContextOptionsBuilder<TrailDbContext>().UseMySQL(connection).Options;
        return new TrailDbContext(options, new NoTenant());
    }

    private sealed class NoTenant : ITenantContext
    {
        public Guid? OrganizationId => null;
        public void UseOrganization(Guid organizationId) { }
    }
}
