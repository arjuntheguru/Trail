using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Trail.Application.Common.Abstractions;
using Trail.Application.Common.Exceptions;

namespace Trail.Infrastructure.Persistence.Sequences;

/// <summary>
/// Uses MySQL's <c>LAST_INSERT_ID(expr)</c>, which is per connection: the upsert advances the counter under the
/// row lock and the same statement batch reads back the value this connection set, so no transaction is needed.
/// </summary>
internal sealed class SequenceAllocator(TrailDbContext db, ITenantContext tenant) : ISequenceAllocator
{
    public async Task<long> NextAsync(string sequence, int count, CancellationToken ct)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(count, 1);
        var organizationId = tenant.OrganizationId
            ?? throw new TenantIsolationException("A sequence can only be advanced inside an organization.");

        var connection = db.Database.GetDbConnection();
        var opened = connection.State != ConnectionState.Open;
        if (opened) await db.Database.OpenConnectionAsync(ct);
        try
        {
            await using var command = connection.CreateCommand();
            command.Transaction = db.Database.CurrentTransaction?.GetDbTransaction();
            command.CommandText =
                """
                INSERT INTO organization_sequences (OrganizationId, Name, Value) VALUES (@organization, @name, LAST_INSERT_ID(@count))
                ON DUPLICATE KEY UPDATE Value = LAST_INSERT_ID(Value + @count);
                SELECT LAST_INSERT_ID();
                """;
            AddParameter(command, "@organization", organizationId);
            AddParameter(command, "@name", sequence);
            AddParameter(command, "@count", count);
            var last = Convert.ToInt64(await command.ExecuteScalarAsync(ct), System.Globalization.CultureInfo.InvariantCulture);
            return last - count + 1;
        }
        finally
        {
            if (opened) await db.Database.CloseConnectionAsync();
        }
    }

    private static void AddParameter(System.Data.Common.DbCommand command, string name, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }
}
