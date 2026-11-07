using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Trail.Domain.Auditing;
using Trail.Infrastructure.Persistence.Conventions;

namespace Trail.Infrastructure.Persistence.Configurations;

internal sealed class AuditEntryConfiguration : IEntityTypeConfiguration<AuditEntry>
{
    public void Configure(EntityTypeBuilder<AuditEntry> builder)
    {
        builder.ToTable("audit_entries");
        builder.Property(x => x.EntityType).HasMaxLength(AuditEntry.EntityTypeMaxLength);
        builder.Property(x => x.Changes).HasJsonConversion();
        builder.Property(x => x.IpAddress).HasMaxLength(64);
        builder.Property(x => x.TraceId).HasMaxLength(32);

        // No foreign keys: the trail must outlive the rows it describes and never block their deletion.
        builder.HasIndex(x => new { x.OrganizationId, x.OccurredAt });
        builder.HasIndex(x => new { x.EntityType, x.EntityId });
        builder.HasIndex(x => new { x.SiteId, x.OccurredAt });
        builder.HasIndex(x => x.ActorId);
    }
}
