using Trail.Domain.Common;

namespace Trail.Domain.Tasks;

public enum TaskKind
{
    /// <summary>Completed once per occurrence, e.g. an opening checklist.</summary>
    Checklist,

    /// <summary>Repeated readings across the day, e.g. a fridge temperature log; each reading is an entry.</summary>
    RecordLog,

    /// <summary>A form filled in when something happens, e.g. an incident report or delivery check.</summary>
    Form,
}

/// <summary>
/// A reusable task definition. Generated tasks copy the fields at creation, so editing a template never
/// rewrites history; <see cref="Version"/> increases whenever the fields change.
/// </summary>
public sealed class TaskTemplate : TenantEntity, ISoftDeletable
{
    public const int NameMaxLength = 200;
    public const int DescriptionMaxLength = 2000;

    public required string Name { get; set; }
    public string? Description { get; set; }
    public TaskKind Kind { get; set; }

    public List<TemplateField> Fields { get; set; } = [];

    /// <summary>A manager must sign off a completed task before it counts as done.</summary>
    public bool RequiresApproval { get; set; }

    /// <summary>At least one photo must be attached to the task (or each record log entry) on completion.</summary>
    public bool RequiresPhoto { get; set; }

    public int Version { get; set; } = 1;
    public bool IsActive { get; set; } = true;

    /// <summary>Key of the built-in library template this was copied from, if any.</summary>
    public string? LibraryKey { get; set; }

    public List<TaskTemplateTag> Tags { get; set; } = [];

    public DateTime? DeletedAt { get; set; }
    public Guid? DeletedById { get; set; }
}

public sealed class TaskTemplateTag : TenantEntity
{
    public Guid TemplateId { get; set; }
    public Guid TagId { get; set; }
}
