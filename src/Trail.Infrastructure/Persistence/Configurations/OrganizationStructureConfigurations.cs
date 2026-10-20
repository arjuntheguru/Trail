using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Trail.Domain.Identity;
using Trail.Domain.Organizations;
using Trail.Infrastructure.Persistence.Conventions;

namespace Trail.Infrastructure.Persistence.Configurations;

internal sealed class RegionConfiguration : IEntityTypeConfiguration<Region>
{
    public void Configure(EntityTypeBuilder<Region> builder)
    {
        builder.ToTable("regions");
        builder.Property(x => x.Name).HasMaxLength(Region.NameMaxLength);
        builder.Property(x => x.Description).HasMaxLength(Region.DescriptionMaxLength);
    }
}

internal sealed class SiteConfiguration : IEntityTypeConfiguration<Site>
{
    public void Configure(EntityTypeBuilder<Site> builder)
    {
        builder.ToTable("sites");
        builder.Property(x => x.Name).HasMaxLength(Site.NameMaxLength);
        builder.Property(x => x.Code).HasMaxLength(Site.CodeMaxLength);
        builder.Property(x => x.TimeZone).HasMaxLength(Site.TimeZoneMaxLength);
        builder.Property(x => x.Phone).HasMaxLength(Site.PhoneMaxLength);
        builder.Property(x => x.Email).HasMaxLength(Site.EmailMaxLength);
        builder.Property(x => x.Address).HasJsonConversion();
        builder.Property(x => x.OpeningHours).HasJsonConversion();
        builder.HasOne(x => x.Region).WithMany().HasForeignKey(x => x.RegionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => x.RegionId);
        builder.HasIndex(x => new { x.OrganizationId, x.Name });
    }
}

internal sealed class DepartmentConfiguration : IEntityTypeConfiguration<Department>
{
    public void Configure(EntityTypeBuilder<Department> builder)
    {
        builder.ToTable("departments");
        builder.Property(x => x.Name).HasMaxLength(Department.NameMaxLength);
    }
}

internal sealed class MembershipSiteConfiguration : IEntityTypeConfiguration<MembershipSite>
{
    public void Configure(EntityTypeBuilder<MembershipSite> builder)
    {
        builder.ToTable("membership_sites");
        builder.HasOne<Membership>().WithMany().HasForeignKey(x => x.MembershipId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<Site>().WithMany().HasForeignKey(x => x.SiteId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.MembershipId, x.SiteId }).IsUnique();
        builder.HasIndex(x => x.SiteId);
    }
}

internal sealed class MembershipRegionConfiguration : IEntityTypeConfiguration<MembershipRegion>
{
    public void Configure(EntityTypeBuilder<MembershipRegion> builder)
    {
        builder.ToTable("membership_regions");
        builder.HasOne<Membership>().WithMany().HasForeignKey(x => x.MembershipId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<Region>().WithMany().HasForeignKey(x => x.RegionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.MembershipId, x.RegionId }).IsUnique();
        builder.HasIndex(x => x.RegionId);
    }
}

internal sealed class MembershipDepartmentConfiguration : IEntityTypeConfiguration<MembershipDepartment>
{
    public void Configure(EntityTypeBuilder<MembershipDepartment> builder)
    {
        builder.ToTable("membership_departments");
        builder.HasOne<Membership>().WithMany().HasForeignKey(x => x.MembershipId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<Department>().WithMany().HasForeignKey(x => x.DepartmentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.MembershipId, x.DepartmentId }).IsUnique();
        builder.HasIndex(x => x.DepartmentId);
    }
}
