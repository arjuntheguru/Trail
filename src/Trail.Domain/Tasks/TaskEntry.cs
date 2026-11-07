using Trail.Domain.Common;

namespace Trail.Domain.Tasks;

/// <summary>
/// Answers recorded against a task. A checklist or form has one entry that is saved progressively and locked
/// when the task completes; a record log gets a new, immutable entry per reading.
/// </summary>
public sealed class TaskEntry : TenantEntity
{
    public Guid SiteTaskId { get; set; }

    public Dictionary<string, FieldAnswer> Answers { get; set; } = [];

    /// <summary>Photos for the task as a whole (template "requires photo").</summary>
    public List<Guid> PhotoIds { get; set; } = [];

    public string? Note { get; set; }

    public List<FieldFailure> Failures { get; set; } = [];
    public bool HasFailures => Failures.Count > 0;

    /// <summary>Null while a checklist is in progress; set when submitted.</summary>
    public DateTime? SubmittedAt { get; set; }
    public Guid? SubmittedById { get; set; }
}
