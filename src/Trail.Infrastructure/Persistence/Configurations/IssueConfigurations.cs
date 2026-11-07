using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Trail.Domain.Identity;
using Trail.Domain.Issues;
using Trail.Domain.Organizations;
using Trail.Domain.Tasks;
using Trail.Infrastructure.Persistence.Conventions;

namespace Trail.Infrastructure.Persistence.Configurations;

internal sealed class IssueConfiguration : IEntityTypeConfiguration<Issue>
{
    public void Configure(EntityTypeBuilder<Issue> builder)
    {
        builder.ToTable("issues");
        builder.Ignore(x => x.IsActive);
        builder.Property(x => x.Title).HasMaxLength(Issue.TitleMaxLength);
        builder.Property(x => x.Description).HasMaxLength(Issue.DescriptionMaxLength);
        builder.Property(x => x.Resolution).HasMaxLength(Issue.NoteMaxLength);
        builder.Property(x => x.FieldKey).HasMaxLength(TemplateField.KeyMaxLength);
        builder.Property(x => x.PhotoIds).HasJsonConversion();
        builder.HasOne<Site>().WithMany().HasForeignKey(x => x.SiteId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Department>().WithMany().HasForeignKey(x => x.DepartmentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<SiteTask>().WithMany().HasForeignKey(x => x.SiteTaskId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<TaskEntry>().WithMany().HasForeignKey(x => x.TaskEntryId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Membership>().WithMany().HasForeignKey(x => x.AssigneeId).OnDelete(DeleteBehavior.SetNull);
        builder.HasMany(x => x.Events).WithOne().HasForeignKey(x => x.IssueId).OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => new { x.OrganizationId, x.Number }).IsUnique();
        builder.HasIndex(x => new { x.SiteId, x.Status });
        builder.HasIndex(x => new { x.SiteTaskId, x.FieldKey });
        builder.HasIndex(x => x.AssigneeId);
    }
}

internal sealed class IssueEventConfiguration : IEntityTypeConfiguration<IssueEvent>
{
    public void Configure(EntityTypeBuilder<IssueEvent> builder)
    {
        builder.ToTable("issue_events");
        builder.Property(x => x.From).HasMaxLength(100);
        builder.Property(x => x.To).HasMaxLength(100);
        builder.Property(x => x.Note).HasMaxLength(Issue.NoteMaxLength);
        builder.Property(x => x.PhotoIds).HasJsonConversion();
        builder.HasIndex(x => new { x.IssueId, x.OccurredAt });
    }
}

internal sealed class OrganizationSequenceConfiguration : IEntityTypeConfiguration<OrganizationSequence>
{
    public void Configure(EntityTypeBuilder<OrganizationSequence> builder)
    {
        builder.ToTable("organization_sequences");
        builder.HasKey(x => new { x.OrganizationId, x.Name });
        builder.Property(x => x.Name).HasMaxLength(64);
        builder.HasOne<Organization>().WithMany().HasForeignKey(x => x.OrganizationId).OnDelete(DeleteBehavior.Cascade);
    }
}
