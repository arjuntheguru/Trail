using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Trail.Infrastructure;

namespace Trail.IntegrationTests.Infrastructure;

/// <summary>The real API wired to a fresh, migrated database. Create one per test class.</summary>
public sealed class TrailApiFactory(string connectionString) : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration(config => config.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Database:ConnectionString"] = connectionString,
            ["Database:MigrateOnStartup"] = "false",
            ["Serilog:MinimumLevel:Default"] = "Warning",
        }));
    }

    public static async Task<TrailApiFactory> CreateAsync(MySqlFixture mysql)
    {
        var factory = new TrailApiFactory(await mysql.CreateDatabaseAsync());
        await factory.Services.MigrateDatabaseAsync();
        return factory;
    }
}
