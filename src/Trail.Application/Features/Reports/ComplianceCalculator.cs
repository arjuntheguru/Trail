using Trail.Domain.Tasks;

namespace Trail.Application.Features.Reports;

public enum ComplianceGrouping
{
    Site,
    Day,
    Template,
}

/// <summary>A count of tasks sharing the same site, template, day and outcome, as read from the database.</summary>
internal sealed record ComplianceBucket(
    Guid SiteId, Guid TemplateId, string Title, DateOnly DueDate, SiteTaskStatus Status,
    bool CompletedLate, bool HasFailures, bool AwaitingApproval, int Count);

internal sealed record SiteInfo(Guid Id, string Name, DateOnly Today);

/// <summary>
/// Turns task buckets into compliance rows. A task counts as due once its local day has ended or it is done;
/// today's still-open tasks are left out so a report run mid-day is not dragged down by work not yet due.
/// Cancelled tasks never count. Only tasks completed on time score.
/// </summary>
internal static class ComplianceCalculator
{
    public static (IReadOnlyList<ComplianceRowResponse> Rows, ComplianceRowResponse Total) Compute(
        IEnumerable<ComplianceBucket> buckets, ComplianceGrouping groupBy, IReadOnlyDictionary<Guid, SiteInfo> sites)
    {
        var groups = new Dictionary<string, Acc>();
        var total = new Acc("total", "Total");

        foreach (var b in buckets)
        {
            if (b.Status == SiteTaskStatus.Cancelled || b.Count <= 0) continue;
            if (!sites.TryGetValue(b.SiteId, out var site)) continue;

            var pastDue = b.DueDate < site.Today;
            if (b.Status == SiteTaskStatus.Open && !pastDue) continue;

            var (key, name) = groupBy switch
            {
                ComplianceGrouping.Site => (site.Id.ToString("D"), site.Name),
                ComplianceGrouping.Template => (b.TemplateId.ToString("D"), b.Title),
                _ => (b.DueDate.ToString("yyyy-MM-dd"), b.DueDate.ToString("yyyy-MM-dd")),
            };
            if (!groups.TryGetValue(key, out var acc)) groups[key] = acc = new Acc(key, name);
            else if (groupBy == ComplianceGrouping.Template && string.CompareOrdinal(b.Title, acc.Name) > 0) acc.Name = b.Title;

            acc.Add(b);
            total.Add(b);
        }

        var rows = groups.Values
            .OrderBy(a => groupBy == ComplianceGrouping.Day ? a.Key : a.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(a => a.Key, StringComparer.Ordinal)
            .Select(a => a.ToResponse())
            .ToList();
        return (rows, total.ToResponse());
    }

    /// <summary>Share of due tasks completed on time as a percentage with one decimal; null when nothing was due.</summary>
    public static decimal? Score(int completedOnTime, int due) =>
        due <= 0 ? null : Math.Round(completedOnTime * 100m / due, 1, MidpointRounding.AwayFromZero);

    private sealed class Acc(string key, string name)
    {
        private int _due, _onTime, _late, _missed, _failures, _awaiting;
        public string Key { get; } = key;
        public string Name { get; set; } = name;

        public void Add(ComplianceBucket b)
        {
            _due += b.Count;
            switch (b.Status)
            {
                case SiteTaskStatus.Completed:
                    if (b.CompletedLate) _late += b.Count; else _onTime += b.Count;
                    if (b.HasFailures) _failures += b.Count;
                    if (b.AwaitingApproval) _awaiting += b.Count;
                    break;
                case SiteTaskStatus.Missed:
                case SiteTaskStatus.Open: // only past-due open tasks reach here
                    _missed += b.Count;
                    break;
            }
        }

        public ComplianceRowResponse ToResponse() =>
            new(Key, Name, _due, _onTime, _late, _missed, _failures, _awaiting, Score(_onTime, _due));
    }
}
