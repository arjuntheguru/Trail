using Trail.Domain.Common;

namespace Trail.Domain.Tasks;

/// <summary>
/// Puts a template on sites' task lists on a recurrence. Targets every site (<see cref="AllSites"/>) or the
/// sites and regions listed; optionally only one department's list. Times are site-local.
/// </summary>
public sealed class TaskSchedule : TenantEntity, ISoftDeletable
{
    public Guid TemplateId { get; set; }
    public TaskTemplate Template { get; set; } = null!;

    public Frequency Frequency { get; set; }
    public int Interval { get; set; } = 1;
    public DateOnly StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public WeekDays Days { get; set; }
    public int? DayOfMonth { get; set; }

    /// <summary>Local time the task is due by on its day.</summary>
    public TimeOnly DueTime { get; set; }

    /// <summary>Local time the task appears; null means from the start of the day.</summary>
    public TimeOnly? AvailableFrom { get; set; }

    public bool AllSites { get; set; }
    public Guid? DepartmentId { get; set; }
    public bool IsActive { get; set; } = true;

    public List<TaskScheduleSite> Sites { get; set; } = [];
    public List<TaskScheduleRegion> Regions { get; set; } = [];

    public DateTime? DeletedAt { get; set; }
    public Guid? DeletedById { get; set; }

    public Recurrence Recurrence => new()
    {
        Frequency = Frequency,
        Interval = Interval,
        StartDate = StartDate,
        EndDate = EndDate,
        Days = Days,
        DayOfMonth = DayOfMonth,
    };
}

public sealed class TaskScheduleSite : TenantEntity
{
    public Guid ScheduleId { get; set; }
    public Guid SiteId { get; set; }
}

public sealed class TaskScheduleRegion : TenantEntity
{
    public Guid ScheduleId { get; set; }
    public Guid RegionId { get; set; }
}
