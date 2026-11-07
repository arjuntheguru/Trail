namespace Trail.Domain.Organizations;

/// <summary>
/// A per-organization counter, e.g. for issue numbers. Only ever advanced atomically by
/// <c>ISequenceAllocator</c>; never loaded and saved through the change tracker.
/// </summary>
public sealed class OrganizationSequence
{
    public const string Issues = "issues";

    public Guid OrganizationId { get; set; }
    public required string Name { get; set; }
    public long Value { get; set; }
}
