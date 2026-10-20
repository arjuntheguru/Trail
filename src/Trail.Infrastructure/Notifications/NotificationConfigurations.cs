using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Trail.Domain.Identity;
using Trail.Domain.Notifications;
using Trail.Domain.Organizations;
using Trail.Infrastructure.Persistence.Conventions;

namespace Trail.Infrastructure.Notifications;

internal sealed class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> builder)
    {
        builder.ToTable("notifications");
        builder.Property(x => x.Title).HasMaxLength(Notification.TitleMaxLength);
        builder.Property(x => x.Body).HasMaxLength(Notification.BodyMaxLength);
        builder.HasOne<Membership>().WithMany().HasForeignKey(x => x.MembershipId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<Site>().WithMany().HasForeignKey(x => x.SiteId).OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(x => new { x.MembershipId, x.ReadAt, x.CreatedAt });
        builder.HasIndex(x => x.EmailStatus);
        builder.HasIndex(x => x.EmailClaimToken);
        builder.HasIndex(x => new { x.SubjectType, x.SubjectId });
    }
}

internal sealed class NotificationPreferenceConfiguration : IEntityTypeConfiguration<NotificationPreference>
{
    public void Configure(EntityTypeBuilder<NotificationPreference> builder)
    {
        builder.ToTable("notification_preferences");
        builder.Property(x => x.EmailKinds).HasJsonConversion();
        builder.HasOne<Membership>().WithMany().HasForeignKey(x => x.MembershipId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(x => x.MembershipId).IsUnique();
    }
}
