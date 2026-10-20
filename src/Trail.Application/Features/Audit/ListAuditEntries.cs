using Microsoft.EntityFrameworkCore;
using Trail.Application.Common.Abstractions;
using Trail.Application.Common.Paging;
using Trail.Domain.Auditing;

namespace Trail.Application.Features.Audit;

public sealed record ListAuditEntriesQuery(
    string? EntityType = null,
    Guid? EntityId = null,
    Guid? ActorId = null,
    Guid? SiteId = null,
    AuditAction? Action = null,
    DateTime? From = null,
    DateTime? To = null);

public sealed record AuditEntryResponse(
    Guid Id,
    DateTime OccurredAt,
    string EntityType,
    Guid EntityId,
    AuditAction Action,
    IReadOnlyDictionary<string, AuditChange> Changes,
    Guid? ActorId,
    string? ActorName,
    Guid? SiteId,
    string? IpAddress,
    string? TraceId);

/// <summary>
/// The organization's change history, newest first. Members limited to some sites see only rows that belong to
/// those sites; organization-wide rows (roles, templates, members) need access to every site.
/// </summary>
public sealed class ListAuditEntriesHandler(IAppDbContext db, ISiteAccess access)
{
    public async Task<PagedResult<AuditEntryResponse>> Handle(ListAuditEntriesQuery query, PageRequest page, CancellationToken ct)
    {
        var scope = await access.GetScopeAsync(ct);
        var entries = db.AuditEntries.AsNoTracking();
        if (!scope.AllSites)
        {
            var siteIds = scope.SiteIds.ToList();
            entries = entries.Where(a => a.SiteId != null && siteIds.Contains(a.SiteId.Value));
        }

        if (!string.IsNullOrWhiteSpace(query.EntityType)) entries = entries.Where(a => a.EntityType == query.EntityType.Trim());
        if (query.EntityId is { } entityId) entries = entries.Where(a => a.EntityId == entityId);
        if (query.ActorId is { } actorId) entries = entries.Where(a => a.ActorId == actorId);
        if (query.SiteId is { } siteId) entries = entries.Where(a => a.SiteId == siteId);
        if (query.Action is { } action) entries = entries.Where(a => a.Action == action);
        if (query.From is { } from) entries = entries.Where(a => a.OccurredAt >= from.ToUniversalTime());
        if (query.To is { } to) entries = entries.Where(a => a.OccurredAt < to.ToUniversalTime());

        var paged = await entries
            .OrderByDescending(a => a.OccurredAt).ThenByDescending(a => a.Id)
            .ToPagedResultAsync(page, ct);

        var actorIds = paged.Items.Where(a => a.ActorId != null).Select(a => a.ActorId!.Value).Distinct().ToList();
        var names = await db.Users.AsNoTracking()
            .Where(u => actorIds.Contains(u.Id))
            .Select(u => new { u.Id, u.FirstName, u.LastName })
            .ToDictionaryAsync(u => u.Id, u => $"{u.FirstName} {u.LastName}".Trim(), ct);

        return new PagedResult<AuditEntryResponse>(
            paged.Items.Select(a => new AuditEntryResponse(
                a.Id, a.OccurredAt, a.EntityType, a.EntityId, a.Action, a.Changes, a.ActorId,
                a.ActorId is { } id && names.TryGetValue(id, out var name) ? name : null,
                a.SiteId, a.IpAddress, a.TraceId)).ToList(),
            paged.Page, paged.PageSize, paged.TotalCount);
    }
}
