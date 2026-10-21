using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Trail.Domain.Organizations;

namespace Trail.Infrastructure.Persistence.Configurations;

internal sealed class OrganizationConfiguration : IEntityTypeConfiguration<Organization>
{
    public void Configure(EntityTypeBuilder<Organization> builder)
    {
        builder.ToTable("organizations");
        builder.Property(x => x.Name).HasMaxLength(Organization.NameMaxLength);
        builder.Property(x => x.Slug).HasMaxLength(Organization.SlugMaxLength);
        builder.Property(x => x.DefaultTimeZone).HasMaxLength(64);

        // Slugs stay reserved after soft delete so old links never resolve to a different customer.
        builder.HasIndex(x => x.Slug).IsUnique();
    }
}
