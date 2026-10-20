using Trail.Domain.Issues;
using Trail.Domain.Tasks;

namespace Trail.Application.Features.Reports;

/// <summary>Tasks of one site and day counted by outcome. <paramref name="Overdue"/> means open and past its due time.</summary>
internal sealed record DashboardTaskBucket(
    Guid SiteId, DateOnly DueDate, SiteTaskStatus Status, bool CompletedLate, bool Overdue, int Count);

/// <summary>Active (open or in progress) issues of one site and priority.</summary>
internal sealed record DashboardIssueBucket(Guid SiteId, IssuePriority Priority, bool Overdue, int Count);

internal static class DashboardCalculator
{
    public static DashboardResponse Compute(
        IReadOnlyList<SiteInfo> sites, IEnumerable<DashboardTaskBucket> tasks, IEnumerable<DashboardIssueBucket> issues)
    {
        var siteById = sites.ToDictionary(s => s.Id);
        var bySite = sites.ToDictionary(s => s.Id, _ => new Acc());

        foreach (var t in tasks)
        {
            if (t.Status == SiteTaskStatus.Cancelled || t.Count <= 0) continue;
            if (!siteById.TryGetValue(t.SiteId, out var site)) continue;
            var acc = bySite[t.SiteId];

            if (t.DueDate == site.Today)
            {
                acc.DueToday += t.Count;
                if (t.Status == SiteTaskStatus.Completed)
                {
                    acc.Completed += t.Count;
                    if (!t.CompletedLate) acc.OnTime += t.Count;
                }
                else if (t.Status == SiteTaskStatus.Open)
                {
                    acc.Remaining += t.Count;
                    if (t.Overdue) acc.Overdue += t.Count;
                }
            }
            else if (t.DueDate == site.Today.AddDays(-1) && t.Status is SiteTaskStatus.Missed or SiteTaskStatus.Open)
            {
                acc.MissedYesterday += t.Count;
            }
        }

        foreach (var i in issues)
        {
            if (!bySite.TryGetValue(i.SiteId, out var acc) || i.Count <= 0) continue;
            acc.OpenIssues += i.Count;
            if (i.Overdue) acc.OverdueIssues += i.Count;
            acc.Priorities[i.Priority] += i.Count;
        }

        var siteRows = sites
            .OrderBy(s => s.Name, StringComparer.OrdinalIgnoreCase).ThenBy(s => s.Id)
            .Select(s => bySite[s.Id].ToSite(s))
            .ToList();

        var total = new Acc();
        foreach (var a in bySite.Values) total.Merge(a);
        return new DashboardResponse(siteRows, total.ToTotals());
    }

    private sealed class Acc
    {
        public int DueToday, Completed, OnTime, Overdue, Remaining, MissedYesterday, OpenIssues, OverdueIssues;

        public Dictionary<IssuePriority, int> Priorities { get; } = Enum.GetValues<IssuePriority>().ToDictionary(p => p, _ => 0);

        public void Merge(Acc o)
        {
            DueToday += o.DueToday; Completed += o.Completed; OnTime += o.OnTime; Overdue += o.Overdue;
            Remaining += o.Remaining; MissedYesterday += o.MissedYesterday;
            OpenIssues += o.OpenIssues; OverdueIssues += o.OverdueIssues;
            foreach (var (p, n) in o.Priorities) Priorities[p] += n;
        }

        private decimal? Score => ComplianceCalculator.Score(OnTime, DueToday);

        public DashboardSiteResponse ToSite(SiteInfo s) => new(
            s.Id, s.Name, s.Today, DueToday, Completed, OnTime, Overdue, Remaining, MissedYesterday, Score,
            OpenIssues, OverdueIssues, new Dictionary<IssuePriority, int>(Priorities));

        public DashboardTotalsResponse ToTotals() => new(
            DueToday, Completed, OnTime, Overdue, Remaining, MissedYesterday, Score,
            OpenIssues, OverdueIssues, new Dictionary<IssuePriority, int>(Priorities));
    }
}
