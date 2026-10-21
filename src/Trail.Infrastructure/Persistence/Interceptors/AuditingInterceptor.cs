using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Trail.Application.Common.Abstractions;
using Trail.Application.Common.Exceptions;
using Trail.Domain.Common;

namespace Trail.Infrastructure.Persistence.Interceptors;

/// <summary>
/// Runs before every save: stamps audit columns and concurrency tokens, turns deletes of soft-deletable
/// entities into updates, and enforces that writes stay inside the active organization.
/// </summary>
internal sealed class AuditingInterceptor(ICurrentUser currentUser, ITenantContext tenant, TimeProvider clock)
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

        foreach (var entry in context.ChangeTracker.Entries().ToList())
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
    }

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
