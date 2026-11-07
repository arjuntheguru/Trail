using Microsoft.Extensions.Options;
using MySql.Data.MySqlClient;
using Trail.Application.Common.Abstractions;
using Trail.Infrastructure.Persistence;

namespace Trail.Infrastructure.Locking;

/// <summary>
/// Distributed lock on MySQL <c>GET_LOCK</c>. The lock belongs to the database session, so each lock holds its own
/// dedicated connection open until disposed (which releases the lock and closes the connection). If the process dies,
/// the server drops the session and with it the lock.
/// </summary>
internal sealed class MySqlDistributedLock(IOptions<DatabaseOptions> options) : IDistributedLock
{
    public const string Prefix = "trail:";
    public const int MaxNameLength = 64; // MySQL's limit for lock names

    /// <summary>Builds the server-side lock name, prefixed so it cannot clash with other users of the server.</summary>
    public static string LockName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("A lock name is required.", nameof(name));
        var full = Prefix + name.Trim();
        if (full.Length > MaxNameLength)
            throw new ArgumentException($"A lock name can be at most {MaxNameLength - Prefix.Length} characters.", nameof(name));
        return full;
    }

    public async Task<IAsyncDisposable?> TryAcquireAsync(string name, TimeSpan wait, CancellationToken ct)
    {
        var lockName = LockName(name);
        var seconds = (int)Math.Ceiling(Math.Max(0, wait.TotalSeconds));

        var connection = new MySqlConnection(options.Value.ConnectionString);
        try
        {
            await connection.OpenAsync(ct);
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT GET_LOCK(@name, @seconds)";
            command.Parameters.AddWithValue("@name", lockName);
            command.Parameters.AddWithValue("@seconds", seconds);
            command.CommandTimeout = seconds + 30; // the server may legitimately block for the whole wait
            var acquired = Convert.ToInt64(await command.ExecuteScalarAsync(ct)) == 1;
            if (acquired) return new Handle(connection, lockName);
        }
        catch
        {
            await connection.DisposeAsync();
            throw;
        }

        await connection.DisposeAsync();
        return null;
    }

    private sealed class Handle(MySqlConnection connection, string lockName) : IAsyncDisposable
    {
        private int _disposed;

        public async ValueTask DisposeAsync()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 1) return;
            try
            {
                await using var command = connection.CreateCommand();
                command.CommandText = "SELECT RELEASE_LOCK(@name)";
                command.Parameters.AddWithValue("@name", lockName);
                await command.ExecuteScalarAsync();
            }
            catch
            {
                // Closing the connection below ends the session, which releases the lock anyway.
            }
            finally
            {
                await connection.DisposeAsync();
            }
        }
    }
}
