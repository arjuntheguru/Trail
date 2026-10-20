namespace Trail.Application.Common.Abstractions;

/// <summary>Hands out gap-tolerant, never-repeating numbers per organization, safely across concurrent requests.</summary>
public interface ISequenceAllocator
{
    /// <summary>Reserves <paramref name="count"/> numbers of the current tenant's sequence and returns the first.</summary>
    /// <remarks>Runs immediately, outside the unit of work: a failed save afterwards leaves a gap, never a repeat.</remarks>
    Task<long> NextAsync(string sequence, int count, CancellationToken ct);
}
