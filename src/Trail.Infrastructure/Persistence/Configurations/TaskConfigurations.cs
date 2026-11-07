using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Trail.Domain.Files;
using Trail.Domain.Organizations;
using Trail.Domain.Tasks;
using Trail.Infrastructure.Persistence.Conventions;

namespace Trail.Infrastructure.Persistence.Configurations;

internal sealed class TaskTemplateConfiguration : IEntityTypeConfiguration<TaskTemplate>
{
    public void Configure(EntityTypeBuilder<TaskTemplate> builder)
    {
        builder.ToTable("task_templates");
        builder.Property(x => x.Name).HasMaxLength(TaskTemplate.NameMaxLength);
        builder.Property(x => x.Description).HasMaxLength(TaskTemplate.DescriptionMaxLength);
        builder.Property(x => x.LibraryKey).HasMaxLength(100);
        builder.Property(x => x.Fields).HasJsonConversion();
        builder.HasMany(x => x.Tags).WithOne().HasForeignKey(x => x.TemplateId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(x => new { x.OrganizationId, x.Name });
    }
}

internal sealed class TaskTemplateTagConfiguration : IEntityTypeConfiguration<TaskTemplateTag>
{
    public void Configure(EntityTypeBuilder<TaskTemplateTag> builder)
    {
        builder.ToTable("task_template_tags");
        builder.HasOne<Tag>().WithMany().HasForeignKey(x => x.TagId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(x => new { x.TemplateId, x.TagId }).IsUnique();
        builder.HasIndex(x => x.TagId);
    }
}

internal sealed class TaskScheduleConfiguration : IEntityTypeConfiguration<TaskSchedule>
{
    public void Configure(EntityTypeBuilder<TaskSchedule> builder)
    {
        builder.ToTable("task_schedules");
        builder.Ignore(x => x.Recurrence);
        // A flags value; stored as its number so "every weekday" does not depend on enum member names.
        builder.Property(x => x.Days).HasConversion<int>();
        builder.HasOne(x => x.Template).WithMany().HasForeignKey(x => x.TemplateId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Department>().WithMany().HasForeignKey(x => x.DepartmentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(x => x.Sites).WithOne().HasForeignKey(x => x.ScheduleId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(x => x.Regions).WithOne().HasForeignKey(x => x.ScheduleId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(x => x.TemplateId);
    }
}

internal sealed class TaskScheduleSiteConfiguration : IEntityTypeConfiguration<TaskScheduleSite>
{
    public void Configure(EntityTypeBuilder<TaskScheduleSite> builder)
    {
        builder.ToTable("task_schedule_sites");
        builder.HasOne<Site>().WithMany().HasForeignKey(x => x.SiteId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.ScheduleId, x.SiteId }).IsUnique();
        builder.HasIndex(x => x.SiteId);
    }
}

internal sealed class TaskScheduleRegionConfiguration : IEntityTypeConfiguration<TaskScheduleRegion>
{
    public void Configure(EntityTypeBuilder<TaskScheduleRegion> builder)
    {
        builder.ToTable("task_schedule_regions");
        builder.HasOne<Region>().WithMany().HasForeignKey(x => x.RegionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.ScheduleId, x.RegionId }).IsUnique();
        builder.HasIndex(x => x.RegionId);
    }
}

internal sealed class SiteTaskConfiguration : IEntityTypeConfiguration<SiteTask>
{
    public void Configure(EntityTypeBuilder<SiteTask> builder)
    {
        builder.ToTable("site_tasks");
        builder.Property(x => x.Title).HasMaxLength(TaskTemplate.NameMaxLength);
        builder.Property(x => x.Description).HasMaxLength(TaskTemplate.DescriptionMaxLength);
        builder.Property(x => x.ApprovalNote).HasMaxLength(SiteTask.NoteMaxLength);
        builder.Property(x => x.Fields).HasJsonConversion();
        builder.HasOne<Site>().WithMany().HasForeignKey(x => x.SiteId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<TaskTemplate>().WithMany().HasForeignKey(x => x.TemplateId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<TaskSchedule>().WithMany().HasForeignKey(x => x.ScheduleId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Department>().WithMany().HasForeignKey(x => x.DepartmentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(x => x.Entries).WithOne().HasForeignKey(x => x.SiteTaskId).OnDelete(DeleteBehavior.Cascade);

        // The generator relies on this to stay idempotent when it runs twice or on two instances at once.
        // Ad hoc tasks have no schedule; MySQL lets NULLs repeat in a unique index.
        builder.HasIndex(x => new { x.ScheduleId, x.SiteId, x.ScheduledDate }).IsUnique();
        builder.HasIndex(x => new { x.SiteId, x.DueDate });
        builder.HasIndex(x => new { x.Status, x.DueAt });
    }
}

internal sealed class TaskEntryConfiguration : IEntityTypeConfiguration<TaskEntry>
{
    public void Configure(EntityTypeBuilder<TaskEntry> builder)
    {
        builder.ToTable("task_entries");
        builder.Ignore(x => x.HasFailures);
        builder.Property(x => x.Answers).HasJsonConversion();
        builder.Property(x => x.PhotoIds).HasJsonConversion();
        builder.Property(x => x.Failures).HasJsonConversion();
        builder.Property(x => x.Note).HasMaxLength(SiteTask.NoteMaxLength);
    }
}

internal sealed class AttachmentConfiguration : IEntityTypeConfiguration<Attachment>
{
    public void Configure(EntityTypeBuilder<Attachment> builder)
    {
        builder.ToTable("attachments");
        builder.Property(x => x.FileName).HasMaxLength(Attachment.FileNameMaxLength);
        builder.Property(x => x.ContentType).HasMaxLength(100);
        builder.Property(x => x.StorageKey).HasMaxLength(300);
        builder.Property(x => x.Sha256).HasMaxLength(64).IsFixedLength();
        builder.HasOne<Site>().WithMany().HasForeignKey(x => x.SiteId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => x.SiteId);
    }
}
