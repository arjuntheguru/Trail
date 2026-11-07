using MySql.Data.MySqlClient;
using Testcontainers.MySql;

[assembly: AssemblyFixture(typeof(Trail.IntegrationTests.Infrastructure.MySqlFixture))]

namespace Trail.IntegrationTests.Infrastructure;

/// <summary>
/// One MySQL 8.4 container for the whole test run. Each test class creates its own database on it. Set
/// <c>TRAIL_TEST_MYSQL</c> to a root connection string (e.g. the docker compose MySQL) to use that server instead
/// of starting a container.
/// </summary>
public sealed class MySqlFixture : IAsyncLifetime
{
    private static readonly string? ExternalServer = Environment.GetEnvironmentVariable("TRAIL_TEST_MYSQL");

    private readonly MySqlContainer _container = new MySqlBuilder("mysql:8.4")
        .WithUsername("root")
        .WithPassword("trail-test")
        // Every test class has its own database and so its own connection pool; idle pooled connections from
        // finished classes add up past the default limit of 151.
        .WithCommand("--max-connections=2000")
        .Build();

    private string ServerConnectionString => ExternalServer ?? _container.GetConnectionString();

    public async ValueTask InitializeAsync()
    {
        if (ExternalServer is null) await _container.StartAsync();
    }

    public async ValueTask DisposeAsync() => await _container.DisposeAsync();

    /// <summary>Creates an empty database and returns a connection string for it.</summary>
    public async Task<string> CreateDatabaseAsync()
    {
        var name = $"trail_{Guid.NewGuid():N}"[..20];
        await using var connection = new MySqlConnection(ServerConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = $"CREATE DATABASE `{name}` CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci";
        await command.ExecuteNonQueryAsync();

        return new MySqlConnectionStringBuilder(ServerConnectionString)
        {
            Database = name,
            // The container is local and unencrypted. Connector/NET's TLS setup is not thread-safe when many
            // test classes open their first connection at once.
            SslMode = MySqlSslMode.Disabled,
            MaximumPoolSize = 20,
        }.ConnectionString;
    }
}
