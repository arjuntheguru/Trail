using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Trail.Domain.Organizations;

namespace Trail.Infrastructure.Persistence.Configurations;

internal sealed class TagConfiguration : IEntityTypeConfiguration<Tag>
{
    public void Configure(EntityTypeBuilder<Tag> builder)
    {
        builder.ToTable("tags");
        builder.Property(x => x.Name).HasMaxLength(Tag.NameMaxLength);
        builder.Property(x => x.Color).HasMaxLength(7);
        builder.HasIndex(x => new { x.OrganizationId, x.Name }).IsUnique();
    }
}
