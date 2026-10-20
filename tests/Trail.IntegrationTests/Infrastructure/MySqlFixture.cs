using MySql.Data.MySqlClient;
using Testcontainers.MySql;

[assembly: AssemblyFixture(typeof(Trail.IntegrationTests.Infrastructure.MySqlFixture))]

namespace Trail.IntegrationTests.Infrastructure;

/// <summary>One MySQL 8.4 container for the whole test run. Each test class creates its own database on it.</summary>
public sealed class MySqlFixture : IAsyncLifetime
{
    private readonly MySqlContainer _container = new MySqlBuilder("mysql:8.4")
        .WithUsername("root")
        .WithPassword("trail-test")
        .Build();

    public async ValueTask InitializeAsync() => await _container.StartAsync();

    public async ValueTask DisposeAsync() => await _container.DisposeAsync();

    /// <summary>Creates an empty database and returns a connection string for it.</summary>
    public async Task<string> CreateDatabaseAsync()
    {
        var name = $"trail_{Guid.NewGuid():N}"[..20];
        await using var connection = new MySqlConnection(_container.GetConnectionString());
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = $"CREATE DATABASE `{name}` CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci";
        await command.ExecuteNonQueryAsync();

        return new MySqlConnectionStringBuilder(_container.GetConnectionString()) { Database = name }.ConnectionString;
    }
}
