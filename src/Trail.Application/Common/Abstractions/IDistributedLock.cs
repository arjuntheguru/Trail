namespace Trail.Application.Common.Abstractions;

/// <summary>A named lock shared by every API replica, used to run singleton work (migrations, scheduled jobs) once.</summary>
public interface IDistributedLock
{
    /// <summary>
    /// Tries to take the named lock, waiting up to <paramref name="wait"/> (zero means do not wait).
    /// Returns null when it was not acquired; otherwise a handle whose disposal releases the lock.
    /// </summary>
    Task<IAsyncDisposable?> TryAcquireAsync(string name, TimeSpan wait, CancellationToken ct);
}