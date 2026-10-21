using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Trail.Application.Common.Abstractions;
using Trail.Application.Common.Results;
using Trail.Application.Features.Audit;

namespace Trail.Application.Features.Reports;

/// <summary>From/To bound the change time (UTC, inclusive) and span at most 93 days.</summary>
public sealed record ExportAuditQuery(DateOnly? From = null, DateOnly? To = null, string? EntityType = null, Guid? SiteId = null);

/// <summary>Same scoping and filters as the audit list; the caller also needs permission to export.</summary>
public sealed class ExportAuditCsvHandler(IAppDbContext db, ISiteAccess access, TimeProvider clock)
{
    public async Task<Result<CsvExport>> Handle(ExportAuditQuery query, CancellationToken ct)
    {
        var range = ReportRange.Resolve(query.From, query.To, DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime));
        if (range.IsFailure) return range.Error!;

        var scope = await access.GetScopeAsync(ct);
        if (await ReportScope.CheckSiteAsync(db, scope, query.SiteId, ct) is { } error) return error;

        var entries = AuditEntryQueries.Filter(
            db.AuditEntries.AsNoTracking(), scope,
            new ListAuditEntriesQuery(EntityType: query.EntityType, SiteId: query.SiteId, From: range.Value.FromUtc, To: range.Value.ToExclusiveUtc));

        if (await entries.CountAsync(ct) > ReportScope.MaxExportRows) return ReportScope.TooManyRows;

        var sites = db.Sites.IgnoreQueryFilters([QueryFilters.SoftDelete]);

        var rows = entries
            .OrderBy(a => a.OccurredAt).ThenBy(a => a.Id)
            .Select(a => new
            {
                a.OccurredAt, a.EntityType, a.EntityId, a.Action,
                Actor = db.Users.Where(u => u.Id == a.ActorId).Select(u => u.FirstName + " " + u.LastName).FirstOrDefault(),
                SiteName = sites.Where(s => s.Id == a.SiteId).Select(s => s.Name).FirstOrDefault(),
                a.IpAddress, a.TraceId, a.Changes,
            });

        return new CsvExport($"audit-{range.Value.FileSuffix}.csv", async (stream, token) =>
        {
            await using var csv = new CsvWriter(stream);
            await csv.WriteRowAsync("Occurred at", "Entity type", "Entity id", "Action", "Actor", "Site", "IP address", "Trace id", "Changes");
            await foreach (var r in rows.AsAsyncEnumerable().WithCancellation(token))
                await csv.WriteRowAsync(
                    r.OccurredAt, r.EntityType, r.EntityId, r.Action, r.Actor?.Trim(), r.SiteName, r.IpAddress, r.TraceId,
                    JsonSerializer.Serialize(r.Changes));
        });
    }
}
