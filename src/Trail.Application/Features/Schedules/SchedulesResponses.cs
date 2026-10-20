using Trail.Domain.Tasks;

namespace Trail.Application.Features.Schedules;

public sealed record ScheduleResponse(
    Guid Id,
    Guid TemplateId,
    string TemplateName,
    Frequency Frequency,
    int Interval,
    DateOnly StartDate,
    DateOnly? EndDate,
    WeekDays Days,
    int? DayOfMonth,
    string Summary,
    TimeOnly DueTime,
    TimeOnly? AvailableFrom,
    bool AllSites,
    IReadOnlyList<Guid> SiteIds,
    IReadOnlyList<Guid> RegionIds,
    Guid? DepartmentId,
    bool IsActive);

internal static class ScheduleMapping
{
    /// <summary>Needs <see cref="TaskSchedule.Template"/>, <see cref="TaskSchedule.Sites"/> and <see cref="TaskSchedule.Regions"/> loaded.</summary>
    public static ScheduleResponse ToResponse(TaskSchedule s) => new(
        s.Id, s.TemplateId, s.Template.Name, s.Frequency, s.Interval, s.StartDate, s.EndDate, s.Days, s.DayOfMonth,
        RecurrenceSummary.Describe(s.Recurrence), s.DueTime, s.AvailableFrom, s.AllSites,
        s.Sites.Select(x => x.SiteId).ToList(), s.Regions.Select(x => x.RegionId).ToList(), s.DepartmentId, s.IsActive);
}
