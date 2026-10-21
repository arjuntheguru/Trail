namespace Trail.Application.Common.Exceptions;

/// <summary>
/// A write would have touched data outside the active organization. This always indicates a bug or an attack,
/// so it is never caught by application code.
/// </summary>
public sealed class TenantIsolationException(string message) : Exception(message);
