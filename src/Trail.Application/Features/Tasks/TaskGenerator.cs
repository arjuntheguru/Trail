using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Trail.Application.Common;
using Trail.Application.Common.Abstractions;
using Trail.Domain.Common;
using Trail.Domain.Tasks;

namespace Trail.Application.Features.Tasks;

/// <summary>
/// Puts scheduled tasks on site lists and closes out days that have ended, for the current tenant. Runs from
/// the background job for every organization and inline after a schedule changes, so it must be idempotent:
/// the unique (schedule, site, scheduled date) index is the final guard when two runs overlap.
/// </summary>
public sealed class TaskGenerator(IAppDbContext db, TimeProvider clock, ILogger<TaskGenerator> logger)
{
    /// <summary>Days generated ahead of each site's today (0 = today only), so tomorrow's list can be viewed.</summary>
    public const int DaysAhead = 1;

    /// <returns>The number of tasks created.</returns>
    public async Task<int> GenerateAsync(CancellationToken ct, Guid? scheduleId = null)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var sites = await db.Sites.AsNoTracking().Where(s => s.IsActive)
            .Select(s => new { s.Id, s.RegionId, s.TimeZone })
            .ToListAsync(ct);
        if (sites.Count == 0) return 0;

        var schedules = await db.TaskSchedules.AsNoTracking()
            .Where(s => s.IsActive && s.Template.IsActive && (scheduleId == null || s.Id == scheduleId))
            .Include(s => s.Template).Include(s => s.Sites).Include(s => s.Regions)
            .ToListAsync(ct);
        if (schedules.Count == 0) return 0;

        var candidates = new List<SiteTask>();
        foreach (var site in sites)
        {
            var zone = SiteTime.Zone(site.TimeZone);
            var today = SiteTime.Today(now, zone);
            foreach (var schedule in schedules)
            {
                var targeted = schedule.AllSites
                    || schedule.Sites.Any(x => x.SiteId == site.Id)
                    || (site.RegionId is { } region && schedule.Regions.Any(x => x.RegionId == region));
                if (!targeted) continue;

                foreach (var date in schedule.Recurrence.Occurrences(today, today.AddDays(DaysAhead)))
                {
                    candidates.Add(SiteTask.FromTemplate(
                        schedule.Template, site.Id, zone, date, schedule.DueTime, schedule.AvailableFrom,
                        SiteTaskSource.Scheduled, schedule.Id, schedule.DepartmentId));
                }
            }
        }
        if (candidates.Count == 0) return 0;

        var scheduleIds = candidates.Select(t => t.ScheduleId!.Value).Distinct().ToList();
        var from = candidates.Min(t => t.ScheduledDate);
        var existing = (await db.SiteTasks.AsNoTracking()
                .Where(t => t.ScheduleId != null && scheduleIds.Contains(t.ScheduleId.Value) && t.ScheduledDate >= from)
                .Select(t => new { t.ScheduleId, t.SiteId, t.ScheduledDate })
                .ToListAsync(ct))
            .Select(t => (t.ScheduleId!.Value, t.SiteId, t.ScheduledDate))
            .ToHashSet();

        var missing = candidates.Where(t => !existing.Contains((t.ScheduleId!.Value, t.SiteId, t.ScheduledDate))).ToList();
        if (missing.Count == 0) return 0;

        db.SiteTasks.AddRange(missing);
        try
        {
            await db.SaveChangesAsync(ct);
            return missing.Count;
        }
        catch (DbUpdateException ex) when (DbErrors.IsUniqueViolation(ex))
        {
            // Another run inserted some of these in the meantime. Retry one at a time and skip the ones it made.
            logger.LogInformation("Concurrent task generation detected; inserting {Count} tasks individually.", missing.Count);
            db.SiteTasks.RemoveRange(missing);
            return await InsertIndividuallyAsync(missing, ct);
        }
    }

    private async Task<int> InsertIndividuallyAsync(List<SiteTask> tasks, CancellationToken ct)
    {
        var created = 0;
        foreach (var task in tasks)
        {
            db.SiteTasks.Add(task);
            try
            {
                await db.SaveChangesAsync(ct);
                created++;
            }
            catch (DbUpdateException ex) when (DbErrors.IsUniqueViolation(ex))
            {
                db.SiteTasks.Remove(task);
            }
        }
        return created;
    }

    /// <summary>Marks open tasks whose site-local day has ended as missed. They can still be completed late.</summary>
    /// <returns>The number of tasks marked missed.</returns>
    public async Task<int> MarkMissedAsync(CancellationToken ct)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var zones = await db.Sites.AsNoTracking()
            .Select(s => new { s.Id, s.TimeZone })
            .ToListAsync(ct);

        var missed = 0;
        foreach (var group in zones.GroupBy(s => SiteTime.Today(now, SiteTime.Zone(s.TimeZone))))
        {
            var today = group.Key;
            var siteIds = group.Select(s => s.Id).ToList();
            // Bulk update: the auditing interceptor does not see it, so stamp the audit columns here.
            missed += await db.SiteTasks
                .Where(t => t.Status == SiteTaskStatus.Open && t.DueDate < today && siteIds.Contains(t.SiteId))
                .ExecuteUpdateAsync(u => u
                    .SetProperty(t => t.Status, SiteTaskStatus.Missed)
                    .SetProperty(t => t.UpdatedAt, now)
                    .SetProperty(t => t.UpdatedById, (Guid?)null)
                    .SetProperty(t => t.ConcurrencyStamp, Guid.NewGuid()), ct);
        }
        return missed;
    }
}
