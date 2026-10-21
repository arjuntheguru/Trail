namespace Trail.Domain.Common;

/// <summary>
/// Keeps an entity type or property out of the audit trail: secrets (password and token hashes) and
/// high-churn bookkeeping (sign-in counters) that would bury the meaningful changes.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Property, Inherited = false)]
public sealed class NotAuditedAttribute : Attribute;
