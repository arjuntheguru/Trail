using System.Collections.Concurrent;
using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Trail.Application.Common.Abstractions;
using Trail.Application.Common.Exceptions;
using Trail.Domain.Auditing;
using Trail.Domain.Common;
using Trail.Domain.Organizations;
using Trail.Infrastructure.Persistence.Conventions;

namespace Trail.Infrastructure.Persistence.Interceptors;

/// <summary>
/// Runs before every save: stamps audit columns and concurrency tokens, turns deletes of soft-deletable
/// entities into updates, enforces that writes stay inside the active organization, and appends an
/// <see cref="AuditEntry"/> per changed row to the same save. Bulk ExecuteUpdate/ExecuteDelete bypass it.
/// </summary>
internal sealed class AuditingInterceptor(
    ICurrentUser currentUser, ITenantContext tenant, TimeProvider clock, IRequestContext? request = null)
    : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        Apply(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        Apply(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void Apply(DbContext? context)
    {
        if (context is null) return;

        var now = clock.GetUtcNow().UtcDateTime;
        var userId = currentUser.UserId;
        var entries = context.ChangeTracker.Entries().ToList();

        foreach (var entry in entries)
        {
            if (entry.Entity is ITenantOwned owned)
                EnforceTenant(entry, owned);

            if (entry is { State: EntityState.Deleted, Entity: ISoftDeletable softDeletable })
            {
                entry.State = EntityState.Modified;
                softDeletable.DeletedAt = now;
                softDeletable.DeletedById = userId;
            }

            if (entry.Entity is not IAuditable auditable) continue;

            switch (entry.State)
            {
                case EntityState.Added:
                    auditable.CreatedAt = now;
                    auditable.CreatedById = userId;
                    break;
                case EntityState.Modified:
                    auditable.UpdatedAt = now;
                    auditable.UpdatedById = userId;
                    auditable.ConcurrencyStamp = Guid.NewGuid();
                    entry.Property(nameof(IAuditable.CreatedAt)).IsModified = false;
                    entry.Property(nameof(IAuditable.CreatedById)).IsModified = false;
                    break;
            }
        }

        WriteAuditTrail(context, entries, now, userId);
    }

    private void WriteAuditTrail(DbContext context, List<EntityEntry> entries, DateTime now, Guid? userId)
    {
        var traceId = Activity.Current?.TraceId.ToString();
        foreach (var entry in entries)
        {
            if (entry.Entity is not Entity entity || IsNotAudited(entry.Metadata.ClrType)) continue;

            var action = entry.State switch
            {
                EntityState.Added => AuditAction.Created,
                EntityState.Deleted => AuditAction.Deleted,
                EntityState.Modified when entry.Entity is ISoftDeletable
                    && entry.Property(nameof(ISoftDeletable.DeletedAt)) is { IsModified: true, OriginalValue: null } => AuditAction.Deleted,
                EntityState.Modified => AuditAction.Updated,
                _ => (AuditAction?)null,
            };
            if (action is null) continue;

            var changes = new Dictionary<string, AuditChange>(StringComparer.Ordinal);
            foreach (var property in entry.Properties)
            {
                var name = property.Metadata.Name;
                if (property.Metadata.IsPrimaryKey() || IgnoredProperties.Contains(name)) continue;
                if (property.Metadata.PropertyInfo is { } info && info.IsDefined(typeof(NotAuditedAttribute))) continue;

                switch (action)
                {
                    case AuditAction.Created when property.CurrentValue is not null:
                        changes[name] = new AuditChange(null, ToJson(property.CurrentValue));
                        break;
                    case AuditAction.Deleted when entry.State == EntityState.Deleted && property.OriginalValue is not null:
                        changes[name] = new AuditChange(ToJson(property.OriginalValue), null);
                        break;
                    case AuditAction.Updated or AuditAction.Deleted when entry.State == EntityState.Modified && property.IsModified:
                        var before = ToJson(property.OriginalValue);
                        var after = ToJson(property.CurrentValue);
                        if (!JsonEquals(before, after)) changes[name] = new AuditChange(before, after);
                        break;
                }
            }

            // An update that only touched unaudited bookkeeping (e.g. a sign-in counter) is not worth a row.
            if (action == AuditAction.Updated && changes.Count == 0) continue;

            context.Add(new AuditEntry
            {
                OrganizationId = entry.Entity switch
                {
                    ITenantOwned owned => owned.OrganizationId,
                    Organization organization => organization.Id,
                    _ => null,
                },
                SiteId = entry.Entity is Site ? entity.Id : entry.Metadata.FindProperty("SiteId") is { } site ? entry.Property(site.Name).CurrentValue as Guid? : null,
                EntityType = entry.Metadata.ClrType.Name,
                EntityId = entity.Id,
                Action = action.Value,
                Changes = changes,
                ActorId = userId,
                OccurredAt = now,
                IpAddress = request?.IpAddress,
                TraceId = traceId,
            });
        }
    }

    private static readonly HashSet<string> IgnoredProperties =
    [
        nameof(IAuditable.CreatedAt), nameof(IAuditable.CreatedById), nameof(IAuditable.UpdatedAt),
        nameof(IAuditable.UpdatedById), nameof(IAuditable.ConcurrencyStamp),
        nameof(ISoftDeletable.DeletedAt), nameof(ISoftDeletable.DeletedById), nameof(ITenantOwned.OrganizationId),
    ];

    private static readonly ConcurrentDictionary<Type, bool> NotAuditedTypes = new();

    private static bool IsNotAudited(Type type) =>
        NotAuditedTypes.GetOrAdd(type, t => t.IsDefined(typeof(NotAuditedAttribute), inherit: false));

    private static JsonElement? ToJson(object? value) =>
        value is null ? null : JsonSerializer.SerializeToElement(value, value.GetType(), JsonColumn.Options);

    private static bool JsonEquals(JsonElement? a, JsonElement? b) =>
        a is null || b is null ? a is null && b is null : a.Value.GetRawText() == b.Value.GetRawText();

    private void EnforceTenant(EntityEntry entry, ITenantOwned owned)
    {
        if (entry.State is EntityState.Unchanged or EntityState.Detached) return;

        var active = tenant.OrganizationId
            ?? throw new TenantIsolationException(
                $"Cannot write {entry.Metadata.ClrType.Name}: no organization is active for this operation.");

        if (entry.State == EntityState.Added)
        {
            if (owned.OrganizationId == Guid.Empty)
                owned.OrganizationId = active;
            else if (owned.OrganizationId != active)
                throw Violation(entry);
            return;
        }

        var original = entry.Property(nameof(ITenantOwned.OrganizationId)).OriginalValue as Guid?;
        if (original != active || owned.OrganizationId != active)
            throw Violation(entry);
    }

    private static TenantIsolationException Violation(EntityEntry entry) =>
        new($"Write to {entry.Metadata.ClrType.Name} crosses the active organization boundary.");
}
