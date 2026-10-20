namespace Trail.Domain.Common;

public interface IAuditable
{
    DateTime CreatedAt { get; set; }
    Guid? CreatedById { get; set; }
    DateTime? UpdatedAt { get; set; }
    Guid? UpdatedById { get; set; }
    Guid ConcurrencyStamp { get; set; }
}

public interface ITenantOwned
{
    Guid OrganizationId { get; set; }
}

/// <summary>Deleting a soft-deletable entity marks it deleted instead of removing the row; queries hide it by default.</summary>
public interface ISoftDeletable
{
    DateTime? DeletedAt { get; set; }
    Guid? DeletedById { get; set; }
}
